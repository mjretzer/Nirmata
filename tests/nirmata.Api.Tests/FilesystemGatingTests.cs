using System.Net;
using System.Net.Http.Json;
using nirmata.Data.Dto.Models.Workspaces;
using nirmata.Data.Dto.Requests.Workspaces;
using Xunit;

namespace nirmata.Api.Tests;

/// <summary>
/// Verifies that the filesystem endpoint enforces workspace-root path containment,
/// rejecting any path that would escape the registered root with 403 Forbidden,
/// while allowing valid contained paths to proceed (returning 404 when the
/// path does not exist on disk, never 403).
/// </summary>
public class FilesystemGatingTests : IClassFixture<nirmataApiFactory>
{
    private readonly HttpClient _client;

    public FilesystemGatingTests(nirmataApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<Guid> RegisterWorkspaceAsync(string name, string path)
    {
        var response = await _client.PostAsJsonAsync("/v1/workspaces", new WorkspaceCreateRequest
        {
            Name = name,
            Path = path
        });
        response.EnsureSuccessStatusCode();
        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceSummary>();
        return workspace!.Id;
    }

    // ── Path traversal → 403 Forbidden ────────────────────────────────────────

    [Fact]
    public async Task GetFile_BackslashSingleLevelTraversal_ReturnsForbidden()
    {
        var workspaceId = await RegisterWorkspaceAsync("Gating-Backslash1", @"C:\Workspaces\gating-backslash1");

        // %5C is URL-encoded '\'. The service normalises backslashes to forward-slashes,
        // producing "../escape" — which escapes the workspace root.
        var response = await _client.GetAsync($"/v1/workspaces/{workspaceId}/files/..%5Cescape");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetFile_BackslashMultiLevelTraversal_ReturnsForbidden()
    {
        var workspaceId = await RegisterWorkspaceAsync("Gating-Backslash2", @"C:\Workspaces\gating-backslash2");

        // ..%5C..%5Cescape → ..\..\ escape → normalised to ../../escape → escapes root.
        var response = await _client.GetAsync($"/v1/workspaces/{workspaceId}/files/..%5C..%5Cescape");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetFile_BackslashDeepTraversal_ReturnsForbidden()
    {
        var workspaceId = await RegisterWorkspaceAsync("Gating-BackslashDeep", @"C:\Workspaces\gating-backslash-deep");

        // Many levels of backslash traversal, attempting to reach a sensitive path.
        var response = await _client.GetAsync(
            $"/v1/workspaces/{workspaceId}/files/..%5C..%5C..%5C..%5CWindows%5Csystem32");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetFile_TraversalAfterSubdirectory_ReturnsForbidden()
    {
        var workspaceId = await RegisterWorkspaceAsync("Gating-Subdir", @"C:\Workspaces\gating-subdir");

        // Starts inside the workspace via a subdir, then escapes with backslash traversal.
        var response = await _client.GetAsync(
            $"/v1/workspaces/{workspaceId}/files/subdir%5C..%5C..%5C..%5Cescape");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetFile_MixedSeparatorTraversal_ReturnsForbidden()
    {
        var workspaceId = await RegisterWorkspaceAsync("Gating-Mixed", @"C:\Workspaces\gating-mixed");

        // Forward-slash segments combined with encoded-backslash traversal.
        // "subdir/.." stays inside root; the final "%5C..%5Cescape" escapes it.
        var response = await _client.GetAsync(
            $"/v1/workspaces/{workspaceId}/files/subdir/..%5C..%5Cescape");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Contained path → NOT forbidden ────────────────────────────────────────

    [Fact]
    public async Task GetFile_ContainedPath_IsNotForbidden()
    {
        var workspaceId = await RegisterWorkspaceAsync("Gating-Valid", @"C:\Workspaces\gating-valid");

        // Path lives inside the workspace root. File doesn't exist on disk,
        // so the expected result is 404 — never 403.
        var response = await _client.GetAsync($"/v1/workspaces/{workspaceId}/files/src/readme.md");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetFile_NestedContainedPath_IsNotForbidden()
    {
        var workspaceId = await RegisterWorkspaceAsync("Gating-Nested", @"C:\Workspaces\gating-nested");

        var response = await _client.GetAsync($"/v1/workspaces/{workspaceId}/files/.aos/spec/project.json");

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
