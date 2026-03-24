<<<<<<< C:/Users/James Lestler/Desktop/Projects/Nirmata/tests/nirmata.Api.Tests/ChatEndpointsTests.cs
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace nirmata.Api.Tests;

/// <summary>
/// Integration tests that verify the chat endpoints fail closed on unknown workspace ids,
/// returning <c>404 Not Found</c> without dispatching any command.
/// </summary>
public sealed class ChatEndpointsTests : IClassFixture<nirmataApiFactory>
{
    private readonly HttpClient _client;

    // A workspace id that is guaranteed not to exist in the in-memory test DB.
    private static readonly Guid _unknownId = Guid.NewGuid();

    public ChatEndpointsTests(nirmataApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── GET /v1/workspaces/{id}/chat ──────────────────────────────────────────

    [Fact]
    public async Task GetSnapshot_UnknownWorkspace_Returns404()
    {
        var response = await _client.GetAsync($"/v1/workspaces/{_unknownId}/chat");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── POST /v1/workspaces/{id}/chat ─────────────────────────────────────────

    [Fact]
    public async Task PostTurn_UnknownWorkspace_ValidBody_Returns404()
    {
        // Workspace validation (404) must take priority over a valid request body.
        var response = await _client.PostAsJsonAsync(
            $"/v1/workspaces/{_unknownId}/chat",
            new { input = "aos status" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostTurn_UnknownWorkspace_CommandInput_Returns404()
    {
        // Explicit AOS command with unknown workspace — no dispatch should occur.
        var response = await _client.PostAsJsonAsync(
            $"/v1/workspaces/{_unknownId}/chat",
            new { input = "execute-plan" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostTurn_UnknownWorkspace_FreeformInput_Returns404()
    {
        // Conversational freeform text with unknown workspace.
        var response = await _client.PostAsJsonAsync(
            $"/v1/workspaces/{_unknownId}/chat",
            new { input = "what is the current project status?" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Guard: malformed input still returns 400 (model validation, not workspace) ──

    [Fact]
    public async Task PostTurn_MissingInputField_Returns400()
    {
        // An invalid request body returns 400 regardless of workspace existence —
        // model validation is a separate gate from workspace validation.
        var response = await _client.PostAsJsonAsync(
            $"/v1/workspaces/{_unknownId}/chat",
            new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
=======
using System.Net;
using System.Net.Http.Json;
using nirmata.Data.Dto.Models.Chat;
using nirmata.Data.Dto.Models.Workspaces;
using nirmata.Data.Dto.Requests.Workspaces;
using Xunit;

namespace nirmata.Api.Tests;

/// <summary>
/// Integration tests that verify the chat endpoints fail closed on unknown workspace ids,
/// returning <c>404 Not Found</c> without dispatching any command.
/// </summary>
public sealed class ChatEndpointsTests : IClassFixture<nirmataApiFactory>
{
    private readonly HttpClient _client;

    // A workspace id that is guaranteed not to exist in the in-memory test DB.
    private static readonly Guid _unknownId = Guid.NewGuid();

    public ChatEndpointsTests(nirmataApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static string CreateTempWorkspace()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nirmata-chat-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task WriteJsonAsync(string path, object data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(data));
    }

    private async Task<Guid> RegisterWorkspaceAsync(string name, string path)
    {
        var response = await _client.PostAsJsonAsync("/v1/workspaces", new WorkspaceCreateRequest
        {
            Name = name,
            Path = path,
        });

        response.EnsureSuccessStatusCode();

        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceSummary>();
        return workspace!.Id;
    }

    // ── GET /v1/workspaces/{id}/chat ──────────────────────────────────────────

    [Fact]
    public async Task GetSnapshot_UnknownWorkspace_Returns404()
    {
        var response = await _client.GetAsync($"/v1/workspaces/{_unknownId}/chat");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── POST /v1/workspaces/{id}/chat ─────────────────────────────────────────

    [Fact]
    public async Task PostTurn_UnknownWorkspace_ValidBody_Returns404()
    {
        // Workspace validation (404) must take priority over a valid request body.
        var response = await _client.PostAsJsonAsync(
            $"/v1/workspaces/{_unknownId}/chat",
            new { input = "aos status" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostTurn_UnknownWorkspace_CommandInput_Returns404()
    {
        // Explicit AOS command with unknown workspace — no dispatch should occur.
        var response = await _client.PostAsJsonAsync(
            $"/v1/workspaces/{_unknownId}/chat",
            new { input = "execute-plan" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostTurn_UnknownWorkspace_FreeformInput_Returns404()
    {
        // Conversational freeform text with unknown workspace.
        var response = await _client.PostAsJsonAsync(
            $"/v1/workspaces/{_unknownId}/chat",
            new { input = "what is the current project status?" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Chat_EndToEnd_AosStatusReturnsRealGateAndTimeline()
    {
        var workspaceRoot = CreateTempWorkspace();

        try
        {
            // Seed a workspace that has enough real AOS state to drive the gate and timeline.
            await WriteJsonAsync(Path.Combine(workspaceRoot, ".aos", "spec", "project.json"), new { name = "Chat E2E" });
            await WriteJsonAsync(Path.Combine(workspaceRoot, ".aos", "spec", "roadmap.json"), new { milestones = Array.Empty<object>() });
            await WriteJsonAsync(Path.Combine(workspaceRoot, ".aos", "spec", "tasks", "TSK-000001", "task.json"), new
            {
                id = "TSK-000001",
                title = "Implement chat e2e",
                phaseId = "PH-0002",
                status = "Planned",
            });
            await WriteJsonAsync(Path.Combine(workspaceRoot, ".aos", "spec", "tasks", "TSK-000001", "plan.json"), new { taskId = "TSK-000001" });
            await WriteJsonAsync(Path.Combine(workspaceRoot, ".aos", "state", "state.json"), new
            {
                position = new { taskId = "TSK-000001", phaseId = "PH-0002", milestoneId = "MS-0001", status = "InProgress" },
                blockers = Array.Empty<object>(),
            });
            await WriteJsonAsync(Path.Combine(workspaceRoot, ".aos", "evidence", "task-evidence", "TSK-000001", "latest.json"), new { status = "pass" });
            await WriteJsonAsync(Path.Combine(workspaceRoot, ".aos", "spec", "tasks", "TSK-000001", "uat.json"), new { status = "failed" });
            await WriteJsonAsync(Path.Combine(workspaceRoot, ".aos", "spec", "phases", "PH-0001", "phase.json"), new { id = "PH-0001", title = "Setup", status = "Done" });
            await WriteJsonAsync(Path.Combine(workspaceRoot, ".aos", "spec", "phases", "PH-0002", "phase.json"), new { id = "PH-0002", title = "Implementation", status = "InProgress" });

            var workspaceId = await RegisterWorkspaceAsync("Chat E2E", workspaceRoot);

            var snapshotResponse = await _client.GetAsync($"/v1/workspaces/{workspaceId}/chat");
            Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);

            var snapshot = await snapshotResponse.Content.ReadFromJsonAsync<ChatSnapshotDto>();
            Assert.NotNull(snapshot);
            Assert.Empty(snapshot!.Messages);
            Assert.NotEmpty(snapshot.CommandSuggestions);
            Assert.NotEmpty(snapshot.QuickActions);
            Assert.Equal("plan-fix", snapshot.CommandSuggestions[0].Command);

            var turnResponse = await _client.PostAsJsonAsync(
                $"/v1/workspaces/{workspaceId}/chat",
                new { input = "aos status" });

            Assert.Equal(HttpStatusCode.OK, turnResponse.StatusCode);

            var turn = await turnResponse.Content.ReadFromJsonAsync<OrchestratorMessageDto>();
            Assert.NotNull(turn);
            Assert.Equal("assistant", turn!.Role);
            Assert.Contains("Workspace Status", turn.Content);
            Assert.Contains("plan-fix", turn.Content);
            Assert.NotNull(turn.Gate);
            Assert.NotNull(turn.Timeline);
            Assert.Equal("plan-fix", turn.NextCommand);
            Assert.Equal("orchestrator", turn.AgentId);
            Assert.NotEmpty(turn.Timeline!.Steps);
            Assert.Equal("PH-0001", turn.Timeline.Steps[0].Id);
            Assert.Equal("completed", turn.Timeline.Steps[0].Status);
            Assert.Equal("PH-0002", turn.Timeline.Steps[1].Id);
            Assert.Equal("active", turn.Timeline.Steps[1].Status);
        }
        finally
        {
            try { Directory.Delete(workspaceRoot, recursive: true); }
            catch { /* best effort */ }
        }
    }

    // ── Guard: malformed input still returns 400 (model validation, not workspace) ──

    [Fact]
    public async Task PostTurn_MissingInputField_Returns400()
    {
        // An invalid request body returns 400 regardless of workspace existence —
        // model validation is a separate gate from workspace validation.
        var response = await _client.PostAsJsonAsync(
            $"/v1/workspaces/{_unknownId}/chat",
            new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
>>>>>>> C:/Users/James Lestler/.windsurf/worktrees/Nirmata/Nirmata-8a8f29ae/tests/nirmata.Api.Tests/ChatEndpointsTests.cs
