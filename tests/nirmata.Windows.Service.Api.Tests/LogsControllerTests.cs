using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using nirmata.Windows.Service.Api;
using Xunit;

namespace nirmata.Windows.Service.Api.Tests;

public class LogsControllerTests : IClassFixture<DaemonApiFactory>
{
    private readonly DaemonApiFactory _factory;
    private readonly HttpClient _client;

    public LogsControllerTests(DaemonApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetLogs_Returns200()
    {
        var response = await _client.GetAsync("/api/v1/logs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetLogs_ReturnsArray()
    {
        var response = await _client.GetAsync("/api/v1/logs");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }

    [Fact]
    public async Task GetLogs_WithTailParameter_Returns200()
    {
        var response = await _client.GetAsync("/api/v1/logs?tail=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetLogs_WithLevelFilter_Returns200()
    {
        var response = await _client.GetAsync("/api/v1/logs?level=warning");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetLogs_TailOne_ReturnsExactlyOneEntry()
    {
        // Add several entries so the buffer is non-empty; tail=1 always returns exactly 1
        var state = _factory.GetRuntimeState();
        state.AddLogEntry(new HostLogEntry { Timestamp = DateTime.UtcNow, Level = "information", Message = "A", Source = "Tests" });
        state.AddLogEntry(new HostLogEntry { Timestamp = DateTime.UtcNow, Level = "information", Message = "B", Source = "Tests" });
        state.AddLogEntry(new HostLogEntry { Timestamp = DateTime.UtcNow, Level = "information", Message = "C", Source = "Tests" });

        var response = await _client.GetAsync("/api/v1/logs?tail=1");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, body.GetArrayLength());
    }

    [Fact]
    public async Task GetLogs_LevelFilter_ReturnsOnlyMatchingLevel()
    {
        var state = _factory.GetRuntimeState();
        state.AddLogEntry(new HostLogEntry { Timestamp = DateTime.UtcNow, Level = "error", Message = "Error occurred", Source = "Tests" });

        var response = await _client.GetAsync("/api/v1/logs?level=error");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // All returned entries must match the requested level
        foreach (var entry in body.EnumerateArray())
        {
            Assert.Equal("error", entry.GetProperty("level").GetString(), ignoreCase: true);
        }
    }
}
