using System.Net;
using System.Net.Http.Json;
using nirmata.Data.Dto.Models.Workspaces;
using nirmata.Data.Dto.Requests.Workspaces;
using Xunit;

namespace nirmata.Api.Tests;

/// <summary>
/// Verifies that <see cref="WorkspaceSummary.Status"/> is derived from live filesystem
/// inspection and correctly reflects the presence or absence of <c>.git/</c> and <c>.aos/</c>.
/// Also verifies that registration and update are rejected for existing paths that are not
/// git-backed.
/// </summary>
public class WorkspaceStatusTests : IClassFixture<nirmataApiFactory>, IDisposable
{
    private readonly HttpClient _client;
    private readonly List<string> _tempDirs = new();

    public WorkspaceStatusTests(nirmataApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try { Directory.Delete(dir, recursive: true); }
            catch { /* best effort */ }
        }
    }

    private string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nirmata-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _tempDirs.Add(path);
        return path;
    }

    /// <summary>Creates a temp dir with a <c>.git/</c> subdirectory so validation passes.</summary>
    private string CreateGitBackedTempDir()
    {
        var path = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(path, ".git"));
        return path;
    }

    private async Task<WorkspaceSummary> RegisterWorkspaceAsync(string name, string path)
    {
        var response = await _client.PostAsJsonAsync("/v1/workspaces", new WorkspaceCreateRequest
        {
            Name = name,
            Path = path
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkspaceSummary>())!;
    }

    // ── Status at registration time ──────────────────────────────────────────

    [Fact]
    public async Task RegisterWorkspace_PathDoesNotExist_StatusIsMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nirmata-nonexistent-{Guid.NewGuid():N}");

        var workspace = await RegisterWorkspaceAsync("Missing Status", path);

        Assert.Equal(WorkspaceStatus.Missing, workspace.Status);
    }

    [Fact]
    public async Task RegisterWorkspace_PathExistsWithGitNoAos_StatusIsNotInitialized()
    {
        var path = CreateGitBackedTempDir(); // has .git/, no .aos/

        var workspace = await RegisterWorkspaceAsync("Not Initialized", path);

        Assert.Equal(WorkspaceStatus.NotInitialized, workspace.Status);
    }

    [Fact]
    public async Task RegisterWorkspace_PathExistsWithGitAndAos_StatusIsInitialized()
    {
        var path = CreateGitBackedTempDir();
        Directory.CreateDirectory(Path.Combine(path, ".aos"));

        var workspace = await RegisterWorkspaceAsync("Initialized", path);

        Assert.Equal(WorkspaceStatus.Initialized, workspace.Status);
    }

    // ── Git-backed validation on registration and update ────────────────────

    [Fact]
    public async Task RegisterWorkspace_PathExistsWithoutGit_ReturnsBadRequest()
    {
        var path = CreateTempDir(); // exists on disk, no .git/

        var response = await _client.PostAsJsonAsync("/v1/workspaces", new WorkspaceCreateRequest
        {
            Name = "No Git",
            Path = path
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateWorkspace_PathExistsWithoutGit_ReturnsBadRequest()
    {
        // Register with a non-existent path (Missing) — passes git check because path doesn't exist.
        var missingPath = Path.Combine(Path.GetTempPath(), $"nirmata-nonexistent-{Guid.NewGuid():N}");
        var workspace = await RegisterWorkspaceAsync("Update Target", missingPath);

        // Try to update to an existing path that has no .git/
        var noGitPath = CreateTempDir();
        var response = await _client.PutAsJsonAsync($"/v1/workspaces/{workspace.Id}", new WorkspaceUpdateRequest
        {
            Path = noGitPath
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Status reflects live filesystem on each read ─────────────────────────

    [Fact]
    public async Task GetWorkspaceById_AosAddedAfterRegistration_StatusBecomesInitialized()
    {
        var path = CreateGitBackedTempDir(); // has .git/, no .aos/
        var workspace = await RegisterWorkspaceAsync("Status Refresh Add", path);
        Assert.Equal(WorkspaceStatus.NotInitialized, workspace.Status);

        Directory.CreateDirectory(Path.Combine(path, ".aos"));

        var response = await _client.GetAsync($"/v1/workspaces/{workspace.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<WorkspaceSummary>();
        Assert.Equal(WorkspaceStatus.Initialized, updated!.Status);
    }

    [Fact]
    public async Task GetWorkspaceById_AosRemovedAfterRegistration_StatusBecomesNotInitialized()
    {
        var path = CreateGitBackedTempDir();
        var aosPath = Path.Combine(path, ".aos");
        Directory.CreateDirectory(aosPath);

        var workspace = await RegisterWorkspaceAsync("Status Refresh Remove", path);
        Assert.Equal(WorkspaceStatus.Initialized, workspace.Status);

        Directory.Delete(aosPath, recursive: true);

        var response = await _client.GetAsync($"/v1/workspaces/{workspace.Id}");
        var updated = await response.Content.ReadFromJsonAsync<WorkspaceSummary>();
        Assert.Equal(WorkspaceStatus.NotInitialized, updated!.Status);
    }

    [Fact]
    public async Task GetWorkspaceById_GitRemovedAfterRegistration_StatusBecomesNotInitialized()
    {
        var path = CreateGitBackedTempDir();
        var gitPath = Path.Combine(path, ".git");
        Directory.CreateDirectory(Path.Combine(path, ".aos"));

        var workspace = await RegisterWorkspaceAsync("Status Refresh Git Remove", path);
        Assert.Equal(WorkspaceStatus.Initialized, workspace.Status);

        Directory.Delete(gitPath, recursive: true);

        var response = await _client.GetAsync($"/v1/workspaces/{workspace.Id}");
        var updated = await response.Content.ReadFromJsonAsync<WorkspaceSummary>();
        Assert.Equal(WorkspaceStatus.NotInitialized, updated!.Status);
    }

    [Fact]
    public async Task RegisterWorkspace_PathExistsWithAosButNoGit_StatusIsNotInitialized_WhenPathHasNoGit()
    {
        // This verifies the status derivation logic: a registered workspace whose path
        // has .aos/ but no .git/ reports not-initialized on read.
        // (Registration itself would reject such a path, so we simulate via a missing-path
        // registration followed by creating .aos/ without .git/.)
        var path = Path.Combine(Path.GetTempPath(), $"nirmata-nonexistent-{Guid.NewGuid():N}");
        var workspace = await RegisterWorkspaceAsync("Aos Only Status", path);
        Assert.Equal(WorkspaceStatus.Missing, workspace.Status);

        // Now create the directory with .aos/ but no .git/
        Directory.CreateDirectory(path);
        _tempDirs.Add(path);
        Directory.CreateDirectory(Path.Combine(path, ".aos"));

        var response = await _client.GetAsync($"/v1/workspaces/{workspace.Id}");
        var updated = await response.Content.ReadFromJsonAsync<WorkspaceSummary>();
        Assert.Equal(WorkspaceStatus.NotInitialized, updated!.Status);
    }

    // ── Status appears in list endpoint ─────────────────────────────────────

    [Fact]
    public async Task GetWorkspaces_StatusReflectsGitAndAosPresenceForEachEntry()
    {
        var pathFull = CreateGitBackedTempDir();
        Directory.CreateDirectory(Path.Combine(pathFull, ".aos")); // .git/ + .aos/ → Initialized
        var pathGitOnly = CreateGitBackedTempDir(); // .git/ only → NotInitialized

        var full = await RegisterWorkspaceAsync("List Initialized", pathFull);
        var gitOnly = await RegisterWorkspaceAsync("List NotInitialized", pathGitOnly);

        var listResponse = await _client.GetAsync("/v1/workspaces");
        var workspaces = await listResponse.Content.ReadFromJsonAsync<List<WorkspaceSummary>>();

        var listedFull = workspaces!.Single(w => w.Id == full.Id);
        var listedGitOnly = workspaces!.Single(w => w.Id == gitOnly.Id);

        Assert.Equal(WorkspaceStatus.Initialized, listedFull.Status);
        Assert.Equal(WorkspaceStatus.NotInitialized, listedGitOnly.Status);
    }
}
