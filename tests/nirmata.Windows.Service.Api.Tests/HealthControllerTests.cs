using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace nirmata.Windows.Service.Api.Tests;

public class HealthControllerTests : IClassFixture<DaemonApiFactory>
{
    private readonly HttpClient _client;

    public HealthControllerTests(DaemonApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_Returns200()
    {
        var response = await _client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetHealth_ReturnsOkTrue()
    {
        var response = await _client.GetAsync("/api/v1/health");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task GetHealth_ReturnsVersion()
    {
        var response = await _client.GetAsync("/api/v1/health");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.TryGetProperty("version", out var version));
        Assert.False(string.IsNullOrEmpty(version.GetString()));
    }

    [Fact]
    public async Task GetHealth_ReturnsUptimeMsAsNumber()
    {
        var response = await _client.GetAsync("/api/v1/health");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.TryGetProperty("uptimeMs", out var uptime));
        Assert.Equal(JsonValueKind.Number, uptime.ValueKind);
    }
}
