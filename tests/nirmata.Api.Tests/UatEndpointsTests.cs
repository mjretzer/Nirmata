using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using nirmata.Data.Dto.Models.Spec;
using nirmata.Data.Dto.Models.Workspaces;
using nirmata.Data.Dto.Requests.Workspaces;
using Xunit;

namespace nirmata.Api.Tests;

/// <summary>
/// Integration tests for <c>GET /v1/workspaces/{wsId}/uat</c>.
/// Covers UAT summary responses and pass/fail derivation at the task and phase level.
/// </summary>
public class UatEndpointsTests : IClassFixture<nirmataApiFactory>, IDisposable
{
    private readonly HttpClient _client;
    private readonly List<string> _tempDirs = [];

    public UatEndpointsTests(nirmataApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nirmata-uat-ep-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _tempDirs.Add(path);
        return path;
    }

    private async Task<Guid> RegisterWorkspaceAsync(string path)
    {
        var response = await _client.PostAsJsonAsync("/v1/workspaces", new WorkspaceCreateRequest
        {
            Name = $"UatTest-{Guid.NewGuid():N}",
            Path = path
        });
        response.EnsureSuccessStatusCode();
        var ws = await response.Content.ReadFromJsonAsync<WorkspaceSummary>();
        return ws!.Id;
    }

    private static string UatDir(string root) => Path.Combine(root, ".aos", "spec", "uat");
    private static string TaskDir(string root, string taskId) => Path.Combine(root, ".aos", "spec", "tasks", taskId);

    private static async Task WriteUatFileAsync(string path, object payload)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(payload));
    }

    // ── Unknown workspace ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetUatSummary_UnknownWorkspaceId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/v1/workspaces/{Guid.NewGuid()}/uat");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Empty workspace ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUatSummary_EmptyWorkspace_ReturnsOkWithEmptySummary()
    {
        var path = CreateTempDir();
        var wsId = await RegisterWorkspaceAsync(path);

        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/uat");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<UatSummaryDto>();
        Assert.NotNull(summary);
        Assert.Empty(summary!.Records);
        Assert.Empty(summary.TaskSummaries);
        Assert.Empty(summary.PhaseSummaries);
    }

    // ── Global UAT records ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetUatSummary_GlobalUatRecords_ReturnedInResponse()
    {
        var path = CreateTempDir();
        var uatDir = UatDir(path);

        await WriteUatFileAsync(Path.Combine(uatDir, "UAT-0001.json"), new
        {
            id = "UAT-0001", taskId = "TSK-000001", phaseId = "PH-0001", status = "passed",
        });
        await WriteUatFileAsync(Path.Combine(uatDir, "UAT-0002.json"), new
        {
            id = "UAT-0002", taskId = "TSK-000002", phaseId = "PH-0001", status = "failed",
        });

        var wsId = await RegisterWorkspaceAsync(path);

        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/uat");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<UatSummaryDto>();
        Assert.NotNull(summary);
        Assert.Equal(2, summary!.Records.Count);
        Assert.Contains(summary.Records, r => r.Id == "UAT-0001" && r.Status == "passed");
        Assert.Contains(summary.Records, r => r.Id == "UAT-0002" && r.Status == "failed");
    }

    // ── Task-level UAT records ─────────────────────────────────────────────────

    [Fact]
    public async Task GetUatSummary_TaskLevelUatRecords_ReturnedInResponse()
    {
        var path = CreateTempDir();
        var taskUatPath = Path.Combine(TaskDir(path, "TSK-000001"), "uat.json");

        await WriteUatFileAsync(taskUatPath, new
        {
            id = "UAT-T001", taskId = "TSK-000001", phaseId = "PH-0001", status = "passed",
        });

        var wsId = await RegisterWorkspaceAsync(path);

        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/uat");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<UatSummaryDto>();
        Assert.NotNull(summary);
        Assert.Single(summary!.Records);
        Assert.Equal("UAT-T001", summary.Records[0].Id);
        Assert.Equal("TSK-000001", summary.Records[0].TaskId);
    }

    // ── Task summary derivation ────────────────────────────────────────────────

    [Fact]
    public async Task GetUatSummary_AllPassedForTask_TaskSummaryIsPassed()
    {
        var path = CreateTempDir();
        var uatDir = UatDir(path);

        await WriteUatFileAsync(Path.Combine(uatDir, "UAT-0001.json"), new
        {
            id = "UAT-0001", taskId = "TSK-000001", status = "passed",
        });
        await WriteUatFileAsync(Path.Combine(uatDir, "UAT-0002.json"), new
        {
            id = "UAT-0002", taskId = "TSK-000001", status = "passed",
        });

        var wsId = await RegisterWorkspaceAsync(path);
        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/uat");

        var summary = await response.Content.ReadFromJsonAsync<UatSummaryDto>();
        Assert.NotNull(summary);
        var taskSummary = Assert.Single(summary!.TaskSummaries);
        Assert.Equal("TSK-000001", taskSummary.TaskId);
        Assert.Equal("passed", taskSummary.Status);
        Assert.Equal(2, taskSummary.RecordCount);
    }

    [Fact]
    public async Task GetUatSummary_AnyFailedRecordForTask_TaskSummaryIsFailed()
    {
        var path = CreateTempDir();
        var uatDir = UatDir(path);

        await WriteUatFileAsync(Path.Combine(uatDir, "UAT-0001.json"), new
        {
            id = "UAT-0001", taskId = "TSK-000001", status = "passed",
        });
        await WriteUatFileAsync(Path.Combine(uatDir, "UAT-0002.json"), new
        {
            id = "UAT-0002", taskId = "TSK-000001", status = "failed",
        });

        var wsId = await RegisterWorkspaceAsync(path);
        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/uat");

        var summary = await response.Content.ReadFromJsonAsync<UatSummaryDto>();
        Assert.NotNull(summary);
        var taskSummary = Assert.Single(summary!.TaskSummaries);
        Assert.Equal("TSK-000001", taskSummary.TaskId);
        Assert.Equal("failed", taskSummary.Status);
    }

    [Fact]
    public async Task GetUatSummary_MultipleTaskIds_ProducesOneSummaryPerTask()
    {
        var path = CreateTempDir();
        var uatDir = UatDir(path);

        await WriteUatFileAsync(Path.Combine(uatDir, "UAT-0001.json"), new
        {
            id = "UAT-0001", taskId = "TSK-000001", status = "passed",
        });
        await WriteUatFileAsync(Path.Combine(uatDir, "UAT-0002.json"), new
        {
            id = "UAT-0002", taskId = "TSK-000002", status = "failed",
        });

        var wsId = await RegisterWorkspaceAsync(path);
        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/uat");

        var summary = await response.Content.ReadFromJsonAsync<UatSummaryDto>();
        Assert.NotNull(summary);
        Assert.Equal(2, summary!.TaskSummaries.Count);
        Assert.Contains(summary.TaskSummaries, s => s.TaskId == "TSK-000001" && s.Status == "passed");
        Assert.Contains(summary.TaskSummaries, s => s.TaskId == "TSK-000002" && s.Status == "failed");
    }

    // ── Phase summary derivation ───────────────────────────────────────────────

    [Fact]
    public async Task GetUatSummary_AllTasksInPhasePass_PhaseSummaryIsPassed()
    {
        var path = CreateTempDir();
        var uatDir = UatDir(path);

        await WriteUatFileAsync(Path.Combine(uatDir, "UAT-0001.json"), new
        {
            id = "UAT-0001", taskId = "TSK-000001", phaseId = "PH-0001", status = "passed",
        });
        await WriteUatFileAsync(Path.Combine(uatDir, "UAT-0002.json"), new
        {
            id = "UAT-0002", taskId = "TSK-000002", phaseId = "PH-0001", status = "passed",
        });

        var wsId = await RegisterWorkspaceAsync(path);
        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/uat");

        var summary = await response.Content.ReadFromJsonAsync<UatSummaryDto>();
        Assert.NotNull(summary);
        var phaseSummary = Assert.Single(summary!.PhaseSummaries);
        Assert.Equal("PH-0001", phaseSummary.PhaseId);
        Assert.Equal("passed", phaseSummary.Status);
        Assert.Contains("TSK-000001", phaseSummary.TaskIds);
        Assert.Contains("TSK-000002", phaseSummary.TaskIds);
    }

    [Fact]
    public async Task GetUatSummary_AnyTaskInPhaseFails_PhaseSummaryIsFailed()
    {
        var path = CreateTempDir();
        var uatDir = UatDir(path);

        await WriteUatFileAsync(Path.Combine(uatDir, "UAT-0001.json"), new
        {
            id = "UAT-0001", taskId = "TSK-000001", phaseId = "PH-0001", status = "passed",
        });
        await WriteUatFileAsync(Path.Combine(uatDir, "UAT-0002.json"), new
        {
            id = "UAT-0002", taskId = "TSK-000002", phaseId = "PH-0001", status = "failed",
        });

        var wsId = await RegisterWorkspaceAsync(path);
        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/uat");

        var summary = await response.Content.ReadFromJsonAsync<UatSummaryDto>();
        Assert.NotNull(summary);
        var phaseSummary = Assert.Single(summary!.PhaseSummaries);
        Assert.Equal("PH-0001", phaseSummary.PhaseId);
        Assert.Equal("failed", phaseSummary.Status);
    }

    [Fact]
    public async Task GetUatSummary_MultiplePhases_ProducesOneSummaryPerPhase()
    {
        var path = CreateTempDir();
        var uatDir = UatDir(path);

        await WriteUatFileAsync(Path.Combine(uatDir, "UAT-0001.json"), new
        {
            id = "UAT-0001", taskId = "TSK-000001", phaseId = "PH-0001", status = "passed",
        });
        await WriteUatFileAsync(Path.Combine(uatDir, "UAT-0002.json"), new
        {
            id = "UAT-0002", taskId = "TSK-000002", phaseId = "PH-0002", status = "failed",
        });

        var wsId = await RegisterWorkspaceAsync(path);
        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/uat");

        var summary = await response.Content.ReadFromJsonAsync<UatSummaryDto>();
        Assert.NotNull(summary);
        Assert.Equal(2, summary!.PhaseSummaries.Count);
        Assert.Contains(summary.PhaseSummaries, s => s.PhaseId == "PH-0001" && s.Status == "passed");
        Assert.Contains(summary.PhaseSummaries, s => s.PhaseId == "PH-0002" && s.Status == "failed");
    }

    // ── Full summary shape ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetUatSummary_MixedPassFail_ResponseContainsRecordsTasksAndPhaseSummaries()
    {
        var path = CreateTempDir();
        var uatDir = UatDir(path);

        // PH-0001: TSK-000001 passes, TSK-000002 fails → phase "failed"
        await WriteUatFileAsync(Path.Combine(uatDir, "UAT-0001.json"), new
        {
            id = "UAT-0001", taskId = "TSK-000001", phaseId = "PH-0001", status = "passed",
        });
        await WriteUatFileAsync(Path.Combine(uatDir, "UAT-0002.json"), new
        {
            id = "UAT-0002", taskId = "TSK-000002", phaseId = "PH-0001", status = "failed",
        });
        // PH-0002: TSK-000003 passes → phase "passed"
        await WriteUatFileAsync(Path.Combine(uatDir, "UAT-0003.json"), new
        {
            id = "UAT-0003", taskId = "TSK-000003", phaseId = "PH-0002", status = "passed",
        });

        var wsId = await RegisterWorkspaceAsync(path);
        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/uat");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<UatSummaryDto>();
        Assert.NotNull(summary);

        // Records
        Assert.Equal(3, summary!.Records.Count);

        // Task summaries
        Assert.Equal(3, summary.TaskSummaries.Count);
        Assert.Contains(summary.TaskSummaries, s => s.TaskId == "TSK-000001" && s.Status == "passed");
        Assert.Contains(summary.TaskSummaries, s => s.TaskId == "TSK-000002" && s.Status == "failed");
        Assert.Contains(summary.TaskSummaries, s => s.TaskId == "TSK-000003" && s.Status == "passed");

        // Phase summaries
        Assert.Equal(2, summary.PhaseSummaries.Count);
        Assert.Contains(summary.PhaseSummaries, s => s.PhaseId == "PH-0001" && s.Status == "failed");
        Assert.Contains(summary.PhaseSummaries, s => s.PhaseId == "PH-0002" && s.Status == "passed");
    }

    // ── Workspace isolation ────────────────────────────────────────────────────

    [Fact]
    public async Task GetUatSummary_TwoWorkspaces_EachSeesOnlyItsOwnRecords()
    {
        var path1 = CreateTempDir();
        var path2 = CreateTempDir();

        await WriteUatFileAsync(Path.Combine(UatDir(path1), "UAT-0001.json"), new
        {
            id = "UAT-WS1", taskId = "TSK-000001", status = "passed",
        });
        await WriteUatFileAsync(Path.Combine(UatDir(path2), "UAT-0002.json"), new
        {
            id = "UAT-WS2", taskId = "TSK-000002", status = "failed",
        });

        var ws1Id = await RegisterWorkspaceAsync(path1);
        var ws2Id = await RegisterWorkspaceAsync(path2);

        var resp1 = await _client.GetAsync($"/v1/workspaces/{ws1Id}/uat");
        var resp2 = await _client.GetAsync($"/v1/workspaces/{ws2Id}/uat");

        var summary1 = await resp1.Content.ReadFromJsonAsync<UatSummaryDto>();
        var summary2 = await resp2.Content.ReadFromJsonAsync<UatSummaryDto>();

        Assert.NotNull(summary1);
        Assert.Single(summary1!.Records);
        Assert.Equal("UAT-WS1", summary1.Records[0].Id);

        Assert.NotNull(summary2);
        Assert.Single(summary2!.Records);
        Assert.Equal("UAT-WS2", summary2.Records[0].Id);
    }
}
