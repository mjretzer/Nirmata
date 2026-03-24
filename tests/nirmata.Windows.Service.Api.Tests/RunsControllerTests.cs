using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using nirmata.Windows.Service.Api;
using Xunit;

namespace nirmata.Windows.Service.Api.Tests;

public class RunsControllerTests : IClassFixture<DaemonApiFactory>
{
    private readonly DaemonApiFactory _factory;
    private readonly HttpClient _client;

    public RunsControllerTests(DaemonApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        // Reset runs before each test to keep tests independent
        factory.GetRuntimeState().Runs.Clear();
    }

    [Fact]
    public async Task GetRuns_EmptyState_Returns200WithEmptyArray()
    {
        var response = await _client.GetAsync("/api/v1/runs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.Equal(0, body.GetArrayLength());
    }

    [Fact]
    public async Task GetRuns_WithSeededRuns_ReturnsAll()
    {
        var state = _factory.GetRuntimeState();
        state.Runs.Add(new RunSummary { RunId = "RUN-001", TaskId = "TSK-000001", Status = "pass", StartedAt = DateTime.UtcNow });
        state.Runs.Add(new RunSummary { RunId = "RUN-002", TaskId = "TSK-000002", Status = "fail", StartedAt = DateTime.UtcNow });

        var response = await _client.GetAsync("/api/v1/runs");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(2, body.GetArrayLength());
    }

    [Fact]
    public async Task GetRuns_TaskIdFilter_ReturnsMatchingRunsOnly()
    {
        var state = _factory.GetRuntimeState();
        state.Runs.Add(new RunSummary { RunId = "RUN-001", TaskId = "TSK-000001", Status = "pass", StartedAt = DateTime.UtcNow });
        state.Runs.Add(new RunSummary { RunId = "RUN-002", TaskId = "TSK-000002", Status = "pass", StartedAt = DateTime.UtcNow });

        var response = await _client.GetAsync("/api/v1/runs?taskId=TSK-000001");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, body.GetArrayLength());
        Assert.Equal("TSK-000001", body[0].GetProperty("taskId").GetString());
    }

    [Fact]
    public async Task GetRuns_StatusFilter_ReturnsMatchingRunsOnly()
    {
        var state = _factory.GetRuntimeState();
        state.Runs.Add(new RunSummary { RunId = "RUN-001", TaskId = "TSK-000001", Status = "pass", StartedAt = DateTime.UtcNow });
        state.Runs.Add(new RunSummary { RunId = "RUN-002", TaskId = "TSK-000002", Status = "fail", StartedAt = DateTime.UtcNow });

        var response = await _client.GetAsync("/api/v1/runs?status=pass");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, body.GetArrayLength());
        Assert.Equal("pass", body[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetRuns_StatusFilter_IsCaseInsensitive()
    {
        var state = _factory.GetRuntimeState();
        state.Runs.Add(new RunSummary { RunId = "RUN-001", TaskId = "TSK-000001", Status = "Pass", StartedAt = DateTime.UtcNow });

        var response = await _client.GetAsync("/api/v1/runs?status=pass");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, body.GetArrayLength());
    }
}
