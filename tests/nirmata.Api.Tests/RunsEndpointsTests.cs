using System.Net;
using System.Net.Http.Json;
using nirmata.Data.Dto.Models.Evidence;
using nirmata.Data.Dto.Models.Workspaces;
using nirmata.Data.Dto.Requests.Workspaces;
using Xunit;

namespace nirmata.Api.Tests;

/// <summary>
/// Integration tests for <c>GET /v1/workspaces/{wsId}/runs</c> and
/// <c>GET /v1/workspaces/{wsId}/runs/{runId}</c>.
/// </summary>
public class RunsEndpointsTests : IClassFixture<nirmataApiFactory>, IDisposable
{
    private readonly HttpClient _client;
    private readonly List<string> _tempDirs = [];

    public RunsEndpointsTests(nirmataApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nirmata-runs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _tempDirs.Add(path);
        return path;
    }

    private async Task<Guid> RegisterWorkspaceAsync(string path)
    {
        var response = await _client.PostAsJsonAsync("/v1/workspaces", new WorkspaceCreateRequest
        {
            Name = $"RunsTest-{Guid.NewGuid():N}",
            Path = path
        });
        response.EnsureSuccessStatusCode();
        var ws = await response.Content.ReadFromJsonAsync<WorkspaceSummary>();
        return ws!.Id;
    }

    private static string RunsDir(string root) => Path.Combine(root, ".aos", "evidence", "runs");

    private static string CreateRunDir(string root, string runId)
    {
        var dir = Path.Combine(RunsDir(root), runId);
        Directory.CreateDirectory(dir);
        return dir;
    }

    // ── GET /v1/workspaces/{wsId}/runs ────────────────────────────────────────

    [Fact]
    public async Task GetRuns_UnknownWorkspace_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/v1/workspaces/{Guid.NewGuid()}/runs");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetRuns_NoRunsDir_ReturnsOkWithEmptyList()
    {
        var path = CreateTempDir();
        var wsId = await RegisterWorkspaceAsync(path);

        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/runs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var runs = await response.Content.ReadFromJsonAsync<List<RunSummaryDto>>();
        Assert.NotNull(runs);
        Assert.Empty(runs!);
    }

    [Fact]
    public async Task GetRuns_WithRunFolders_ReturnsRunSummaries()
    {
        var path = CreateTempDir();
        Directory.CreateDirectory(RunsDir(path));

        var run1Dir = CreateRunDir(path, "RUN-2026-01-13T010000Z");
        await File.WriteAllTextAsync(Path.Combine(run1Dir, "summary.json"), """
            {"runId":"RUN-2026-01-13T010000Z","taskId":"TSK-000001","status":"pass","timestamp":"2026-01-13T01:00:00Z"}
            """);

        var run2Dir = CreateRunDir(path, "RUN-2026-01-13T020000Z");
        await File.WriteAllTextAsync(Path.Combine(run2Dir, "summary.json"), """
            {"runId":"RUN-2026-01-13T020000Z","taskId":"TSK-000002","status":"fail","timestamp":"2026-01-13T02:00:00Z"}
            """);

        var wsId = await RegisterWorkspaceAsync(path);

        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/runs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var runs = await response.Content.ReadFromJsonAsync<List<RunSummaryDto>>();
        Assert.NotNull(runs);
        Assert.Equal(2, runs!.Count);
        // OrderDescending by folder name: RUN-2026-01-13T020000Z comes first
        Assert.Equal("RUN-2026-01-13T020000Z", runs[0].Id);
        Assert.Equal("TSK-000002", runs[0].TaskId);
        Assert.Equal("fail", runs[0].Status);
        Assert.Equal("RUN-2026-01-13T010000Z", runs[1].Id);
        Assert.Equal("pass", runs[1].Status);
    }

    [Fact]
    public async Task GetRuns_RunFolderWithoutSummary_StillIncludesRun()
    {
        var path = CreateTempDir();
        CreateRunDir(path, "RUN-2026-01-13T030000Z");
        var wsId = await RegisterWorkspaceAsync(path);

        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/runs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var runs = await response.Content.ReadFromJsonAsync<List<RunSummaryDto>>();
        Assert.NotNull(runs);
        Assert.Single(runs!);
        Assert.Equal("RUN-2026-01-13T030000Z", runs![0].Id);
        Assert.Null(runs[0].Status);
    }

    // ── GET /v1/workspaces/{wsId}/runs/{runId} ────────────────────────────────

    [Fact]
    public async Task GetRun_UnknownWorkspace_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/v1/workspaces/{Guid.NewGuid()}/runs/RUN-2026-01-13T010000Z");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetRun_UnknownRunId_ReturnsNotFound()
    {
        var path = CreateTempDir();
        var wsId = await RegisterWorkspaceAsync(path);

        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/runs/RUN-does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetRun_WithFullRunFolder_ReturnsRunDetail()
    {
        var path = CreateTempDir();
        const string runId = "RUN-2026-01-13T021500Z";
        var runDir = CreateRunDir(path, runId);

        await File.WriteAllTextAsync(Path.Combine(runDir, "summary.json"), """
            {"runId":"RUN-2026-01-13T021500Z","taskId":"TSK-000003","status":"pass","timestamp":"2026-01-13T02:15:00Z"}
            """);
        await File.WriteAllTextAsync(Path.Combine(runDir, "commands.json"), """
            {"commands":["dotnet build src/","dotnet test tests/"]}
            """);

        var logsDir = Path.Combine(runDir, "logs");
        Directory.CreateDirectory(logsDir);
        await File.WriteAllTextAsync(Path.Combine(logsDir, "build.log"), "Build succeeded.");
        await File.WriteAllTextAsync(Path.Combine(logsDir, "test.log"), "All tests passed.");

        var artifactsDir = Path.Combine(runDir, "artifacts");
        Directory.CreateDirectory(artifactsDir);
        await File.WriteAllTextAsync(Path.Combine(artifactsDir, "output.json"), "{}");

        var wsId = await RegisterWorkspaceAsync(path);

        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/runs/{runId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var run = await response.Content.ReadFromJsonAsync<RunDetailDto>();
        Assert.NotNull(run);
        Assert.Equal(runId, run!.Id);
        Assert.Equal("TSK-000003", run.TaskId);
        Assert.Equal("pass", run.Status);
        Assert.Equal(2, run.Commands.Count);
        Assert.Contains("dotnet build src/", run.Commands);
        Assert.Equal(2, run.LogFiles.Count);
        Assert.Contains("build.log", run.LogFiles);
        Assert.Contains("test.log", run.LogFiles);
        Assert.Single(run.Artifacts);
        Assert.Equal("output.json", run.Artifacts[0]);
    }

    [Fact]
    public async Task GetRun_RunFolderWithOnlySummary_ReturnsDetailWithEmptyLists()
    {
        var path = CreateTempDir();
        const string runId = "RUN-2026-01-13T040000Z";
        var runDir = CreateRunDir(path, runId);
        await File.WriteAllTextAsync(Path.Combine(runDir, "summary.json"), """
            {"runId":"RUN-2026-01-13T040000Z","taskId":"TSK-000004","status":"pass","timestamp":"2026-01-13T04:00:00Z"}
            """);
        var wsId = await RegisterWorkspaceAsync(path);

        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/runs/{runId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var run = await response.Content.ReadFromJsonAsync<RunDetailDto>();
        Assert.NotNull(run);
        Assert.Equal(runId, run!.Id);
        Assert.Equal("TSK-000004", run.TaskId);
        Assert.Empty(run.Commands);
        Assert.Empty(run.LogFiles);
        Assert.Empty(run.Artifacts);
    }

    [Fact]
    public async Task GetRun_RunFolderWithNoSummary_ReturnsDetailUsingFolderNameAsId()
    {
        var path = CreateTempDir();
        const string runId = "RUN-2026-01-13T050000Z";
        CreateRunDir(path, runId);
        var wsId = await RegisterWorkspaceAsync(path);

        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/runs/{runId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var run = await response.Content.ReadFromJsonAsync<RunDetailDto>();
        Assert.NotNull(run);
        Assert.Equal(runId, run!.Id);
        Assert.Null(run.TaskId);
        Assert.Null(run.Status);
    }
}
