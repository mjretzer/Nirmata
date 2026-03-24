using System.Net;
using System.Net.Http.Json;
using nirmata.Data.Dto.Models.State;
using nirmata.Data.Dto.Models.Workspaces;
using nirmata.Data.Dto.Requests.Workspaces;
using Xunit;

namespace nirmata.Api.Tests;

/// <summary>
/// Integration tests for the Phase 5 continuity endpoints:
/// <c>GET /v1/workspaces/{wsId}/state</c>, <c>/state/handoff</c>, <c>/state/events</c>,
/// <c>/checkpoints</c>, and <c>/state/packs</c>.
/// </summary>
public class ContinuityEndpointsTests : IClassFixture<nirmataApiFactory>, IDisposable
{
    private readonly HttpClient _client;
    private readonly List<string> _tempDirs = [];

    public ContinuityEndpointsTests(nirmataApiFactory factory)
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
        var path = Path.Combine(Path.GetTempPath(), $"nirmata-cont-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _tempDirs.Add(path);
        return path;
    }

    private async Task<Guid> RegisterWorkspaceAsync(string path)
    {
        var response = await _client.PostAsJsonAsync("/v1/workspaces", new WorkspaceCreateRequest
        {
            Name = $"ContinuityTest-{Guid.NewGuid():N}",
            Path = path
        });
        response.EnsureSuccessStatusCode();
        var ws = await response.Content.ReadFromJsonAsync<WorkspaceSummary>();
        return ws!.Id;
    }

    private static string StateDir(string root) => Path.Combine(root, ".aos", "state");
    private static string ContextPacksDir(string root) => Path.Combine(root, ".aos", "context", "packs");

    // ── GET /v1/workspaces/{wsId}/state ──────────────────────────────────────

    [Fact]
    public async Task GetState_UnknownWorkspace_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/v1/workspaces/{Guid.NewGuid()}/state");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetState_NoStateFile_ReturnsNotFound()
    {
        var path = CreateTempDir();
        var wsId = await RegisterWorkspaceAsync(path);

        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/state");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetState_WithStateFile_ReturnsOkWithMappedState()
    {
        var path = CreateTempDir();
        Directory.CreateDirectory(StateDir(path));
        await File.WriteAllTextAsync(Path.Combine(StateDir(path), "state.json"), """
            {
              "position": { "milestoneId": "MS-0001", "phaseId": "PH-0001", "taskId": "TSK-000001", "stepIndex": 1, "status": "InProgress" },
              "decisions": [{ "id": "DEC-001", "topic": "Use SQLite", "decision": "Use SQLite", "rationale": "Simplicity", "timestamp": "2026-01-13T02:15:00Z" }],
              "blockers": [{ "id": "BLK-001", "description": "Waiting for key", "affectedTask": "TSK-000002", "timestamp": "2026-01-13T02:20:00Z" }],
              "lastTransition": { "from": "planned", "to": "InProgress", "timestamp": "2026-01-13T02:15:00Z", "trigger": "execute-plan" }
            }
            """);
        var wsId = await RegisterWorkspaceAsync(path);

        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/state");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await response.Content.ReadFromJsonAsync<ContinuityStateDto>();
        Assert.NotNull(state);
        Assert.Equal("MS-0001", state!.Position?.MilestoneId);
        Assert.Equal("PH-0001", state.Position?.PhaseId);
        Assert.Equal("TSK-000001", state.Position?.TaskId);
        Assert.Equal(1, state.Position?.StepIndex);
        Assert.Equal("InProgress", state.Position?.Status);
        Assert.Single(state.Decisions);
        Assert.Equal("DEC-001", state.Decisions[0].Id);
        Assert.Equal("Use SQLite", state.Decisions[0].Topic);
        Assert.Single(state.Blockers);
        Assert.Equal("BLK-001", state.Blockers[0].Id);
        Assert.Equal("execute-plan", state.LastTransition?.Trigger);
        Assert.Equal("planned", state.LastTransition?.From);
        Assert.Equal("InProgress", state.LastTransition?.To);
    }

    [Fact]
    public async Task GetState_EmptyStateFile_ReturnsNotFound()
    {
        var path = CreateTempDir();
        Directory.CreateDirectory(StateDir(path));
        await File.WriteAllTextAsync(Path.Combine(StateDir(path), "state.json"), "null");
        var wsId = await RegisterWorkspaceAsync(path);

        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/state");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── GET /v1/workspaces/{wsId}/state/handoff ───────────────────────────────

    [Fact]
    public async Task GetHandoff_UnknownWorkspace_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/v1/workspaces/{Guid.NewGuid()}/state/handoff");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetHandoff_NoHandoffFile_ReturnsNotFound()
    {
        var path = CreateTempDir();
        var wsId = await RegisterWorkspaceAsync(path);

        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/state/handoff");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetHandoff_WithHandoffFile_ReturnsOkWithMappedHandoff()
    {
        var path = CreateTempDir();
        Directory.CreateDirectory(StateDir(path));
        await File.WriteAllTextAsync(Path.Combine(StateDir(path), "handoff.json"), """
            {
              "cursor": { "milestoneId": "MS-0001", "phaseId": "PH-0001", "taskId": "TSK-000001", "stepIndex": 2, "status": "InProgress" },
              "inFlightTask": "TSK-000001",
              "inFlightStep": 2,
              "allowedScope": ["src/Foo.cs", "src/Bar.cs"],
              "pendingVerification": true,
              "nextCommand": "verify-work PH-0001",
              "timestamp": "2026-01-13T03:00:00Z"
            }
            """);
        var wsId = await RegisterWorkspaceAsync(path);

        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/state/handoff");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var handoff = await response.Content.ReadFromJsonAsync<HandoffSnapshotDto>();
        Assert.NotNull(handoff);
        Assert.Equal("MS-0001", handoff!.Cursor?.MilestoneId);
        Assert.Equal("TSK-000001", handoff.InFlightTask);
        Assert.Equal(2, handoff.InFlightStep);
        Assert.Equal(2, handoff.AllowedScope.Count);
        Assert.True(handoff.PendingVerification);
        Assert.Equal("verify-work PH-0001", handoff.NextCommand);
    }

    // ── GET /v1/workspaces/{wsId}/state/events ────────────────────────────────

    [Fact]
    public async Task GetEvents_UnknownWorkspace_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/v1/workspaces/{Guid.NewGuid()}/state/events");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_NoEventsFile_ReturnsOkWithEmptyList()
    {
        var path = CreateTempDir();
        var wsId = await RegisterWorkspaceAsync(path);

        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/state/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var events = await response.Content.ReadFromJsonAsync<List<StateEventDto>>();
        Assert.NotNull(events);
        Assert.Empty(events!);
    }

    [Fact]
    public async Task GetEvents_WithEventsFile_ReturnsAllEvents()
    {
        var path = CreateTempDir();
        Directory.CreateDirectory(StateDir(path));
        await File.WriteAllTextAsync(Path.Combine(StateDir(path), "events.ndjson"), """
            {"type":"roadmap.created","timestamp":"2026-01-13T01:00:00Z","payload":{},"references":[]}
            {"type":"phase.planned","timestamp":"2026-01-13T02:00:00Z","payload":{},"references":["PH-0001"]}
            """);
        var wsId = await RegisterWorkspaceAsync(path);

        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/state/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var events = await response.Content.ReadFromJsonAsync<List<StateEventDto>>();
        Assert.NotNull(events);
        Assert.Equal(2, events!.Count);
        Assert.Equal("roadmap.created", events[0].Type);
        Assert.Equal("phase.planned", events[1].Type);
    }

    [Fact]
    public async Task GetEvents_LimitQueryParam_ReturnsTailOfEvents()
    {
        var path = CreateTempDir();
        Directory.CreateDirectory(StateDir(path));

        var lines = Enumerable.Range(1, 5)
            .Select(i => $"{{\"type\":\"event.{i:D2}\",\"timestamp\":\"2026-01-13T0{i}:00:00Z\",\"payload\":{{}},\"references\":[]}}");
        await File.WriteAllTextAsync(Path.Combine(StateDir(path), "events.ndjson"), string.Join("\n", lines));
        var wsId = await RegisterWorkspaceAsync(path);

        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/state/events?limit=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var events = await response.Content.ReadFromJsonAsync<List<StateEventDto>>();
        Assert.NotNull(events);
        Assert.Equal(2, events!.Count);
        // tail of 5 events with limit=2 gives the last two
        Assert.Equal("event.04", events[0].Type);
        Assert.Equal("event.05", events[1].Type);
    }

    [Fact]
    public async Task GetEvents_SkipsMalformedLines_ReturnsValidEvents()
    {
        var path = CreateTempDir();
        Directory.CreateDirectory(StateDir(path));
        await File.WriteAllTextAsync(Path.Combine(StateDir(path), "events.ndjson"), """
            {"type":"roadmap.created","timestamp":"2026-01-13T01:00:00Z","payload":{},"references":[]}
            not-valid-json
            {"type":"phase.planned","timestamp":"2026-01-13T02:00:00Z","payload":{},"references":[]}
            """);
        var wsId = await RegisterWorkspaceAsync(path);

        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/state/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var events = await response.Content.ReadFromJsonAsync<List<StateEventDto>>();
        Assert.NotNull(events);
        Assert.Equal(2, events!.Count);
    }

    // ── GET /v1/workspaces/{wsId}/checkpoints ─────────────────────────────────

    [Fact]
    public async Task GetCheckpoints_UnknownWorkspace_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/v1/workspaces/{Guid.NewGuid()}/checkpoints");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCheckpoints_NoCheckpointsDir_ReturnsOkWithEmptyList()
    {
        var path = CreateTempDir();
        var wsId = await RegisterWorkspaceAsync(path);

        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/checkpoints");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var checkpoints = await response.Content.ReadFromJsonAsync<List<CheckpointSummaryDto>>();
        Assert.NotNull(checkpoints);
        Assert.Empty(checkpoints!);
    }

    [Fact]
    public async Task GetCheckpoints_WithCheckpoints_ReturnsCheckpointSummaries()
    {
        var path = CreateTempDir();
        var checkpointsDir = Path.Combine(StateDir(path), "checkpoints");
        Directory.CreateDirectory(checkpointsDir);
        await File.WriteAllTextAsync(Path.Combine(checkpointsDir, "2026-01-13T021500Z.json"), """
            {
              "position": { "milestoneId": "MS-0001", "phaseId": "PH-0001", "taskId": null, "stepIndex": null, "status": "planned" },
              "timestamp": "2026-01-13T02:15:00Z"
            }
            """);
        await File.WriteAllTextAsync(Path.Combine(checkpointsDir, "2026-01-14T100000Z.json"), """
            {
              "position": { "milestoneId": "MS-0001", "phaseId": "PH-0002", "taskId": "TSK-000003", "stepIndex": 1, "status": "InProgress" },
              "timestamp": "2026-01-14T10:00:00Z"
            }
            """);
        var wsId = await RegisterWorkspaceAsync(path);

        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/checkpoints");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var checkpoints = await response.Content.ReadFromJsonAsync<List<CheckpointSummaryDto>>();
        Assert.NotNull(checkpoints);
        Assert.Equal(2, checkpoints!.Count);
        // newest first (OrderByDescending on filename)
        Assert.Equal("2026-01-14T100000Z", checkpoints[0].Id);
        Assert.Equal("PH-0002", checkpoints[0].Position?.PhaseId);
        Assert.Equal("2026-01-13T021500Z", checkpoints[1].Id);
        Assert.Equal("PH-0001", checkpoints[1].Position?.PhaseId);
    }

    // ── GET /v1/workspaces/{wsId}/state/packs ─────────────────────────────────

    [Fact]
    public async Task GetContextPacks_UnknownWorkspace_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/v1/workspaces/{Guid.NewGuid()}/state/packs");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetContextPacks_NoPacksDir_ReturnsOkWithEmptyList()
    {
        var path = CreateTempDir();
        var wsId = await RegisterWorkspaceAsync(path);

        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/state/packs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var packs = await response.Content.ReadFromJsonAsync<List<ContextPackSummaryDto>>();
        Assert.NotNull(packs);
        Assert.Empty(packs!);
    }

    [Fact]
    public async Task GetContextPacks_WithPacks_ReturnsPackSummaries()
    {
        var path = CreateTempDir();
        Directory.CreateDirectory(ContextPacksDir(path));
        await File.WriteAllTextAsync(Path.Combine(ContextPacksDir(path), "TSK-000001.json"), """
            {
              "packId": "TSK-000001",
              "mode": "execute",
              "budgetTokens": 8000,
              "artifacts": [
                {"order":1,"path":".aos/spec/project.json","role":"project-vision"},
                {"order":2,"path":".aos/state/state.json","role":"current-position"}
              ]
            }
            """);
        await File.WriteAllTextAsync(Path.Combine(ContextPacksDir(path), "PH-0001.json"), """
            {
              "packId": "PH-0001",
              "mode": "plan",
              "budgetTokens": 4000,
              "artifacts": [{"order":1,"path":".aos/spec/roadmap.json","role":"roadmap"}]
            }
            """);
        var wsId = await RegisterWorkspaceAsync(path);

        var response = await _client.GetAsync($"/v1/workspaces/{wsId}/state/packs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var packs = await response.Content.ReadFromJsonAsync<List<ContextPackSummaryDto>>();
        Assert.NotNull(packs);
        Assert.Equal(2, packs!.Count);
        var tskPack = packs.Single(p => p.PackId == "TSK-000001");
        Assert.Equal("execute", tskPack.Mode);
        Assert.Equal(8000, tskPack.BudgetTokens);
        Assert.Equal(2, tskPack.ArtifactCount);
        var phPack = packs.Single(p => p.PackId == "PH-0001");
        Assert.Equal("plan", phPack.Mode);
        Assert.Equal(1, phPack.ArtifactCount);
    }
}
