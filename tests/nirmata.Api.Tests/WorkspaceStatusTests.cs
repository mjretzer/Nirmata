using System.Net;
using System.Net.Http.Json;
using nirmata.Data.Dto.Models.Workspaces;
using nirmata.Data.Dto.Requests.Workspaces;
using Xunit;

namespace nirmata.Api.Tests;

/// <summary>
/// Verifies that <see cref="WorkspaceSummary.Status"/> is derived from live filesystem
/// inspection and correctly reflects the presence or absence of the <c>.aos/</c> directory.
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
    public async Task RegisterWorkspace_PathExistsWithoutAos_StatusIsNotInitialized()
    {
        var path = CreateTempDir();

        var workspace = await RegisterWorkspaceAsync("Not Initialized", path);

        Assert.Equal(WorkspaceStatus.NotInitialized, workspace.Status);
    }

    [Fact]
    public async Task RegisterWorkspace_PathExistsWithAos_StatusIsInitialized()
    {
        var path = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(path, ".aos"));

        var workspace = await RegisterWorkspaceAsync("Initialized", path);

        Assert.Equal(WorkspaceStatus.Initialized, workspace.Status);
    }

    // ── Status reflects live filesystem on each read ─────────────────────────

    [Fact]
    public async Task GetWorkspaceById_AosAddedAfterRegistration_StatusBecomesInitialized()
    {
        var path = CreateTempDir();
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
        var path = CreateTempDir();
        var aosPath = Path.Combine(path, ".aos");
        Directory.CreateDirectory(aosPath);

        var workspace = await RegisterWorkspaceAsync("Status Refresh Remove", path);
        Assert.Equal(WorkspaceStatus.Initialized, workspace.Status);

        Directory.Delete(aosPath, recursive: true);

        var response = await _client.GetAsync($"/v1/workspaces/{workspace.Id}");
        var updated = await response.Content.ReadFromJsonAsync<WorkspaceSummary>();
        Assert.Equal(WorkspaceStatus.NotInitialized, updated!.Status);
    }

    // ── Status appears in list endpoint ─────────────────────────────────────

    [Fact]
    public async Task GetWorkspaces_StatusReflectsAosPresenceForEachEntry()
    {
        var pathWithAos = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(pathWithAos, ".aos"));
        var pathWithoutAos = CreateTempDir();

        var withAos = await RegisterWorkspaceAsync("List Initialized", pathWithAos);
        var withoutAos = await RegisterWorkspaceAsync("List NotInitialized", pathWithoutAos);

        var listResponse = await _client.GetAsync("/v1/workspaces");
        var workspaces = await listResponse.Content.ReadFromJsonAsync<List<WorkspaceSummary>>();

        var listedWithAos = workspaces!.Single(w => w.Id == withAos.Id);
        var listedWithoutAos = workspaces!.Single(w => w.Id == withoutAos.Id);

        Assert.Equal(WorkspaceStatus.Initialized, listedWithAos.Status);
        Assert.Equal(WorkspaceStatus.NotInitialized, listedWithoutAos.Status);
    }
}
