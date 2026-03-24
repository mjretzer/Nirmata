using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace nirmata.Windows.Service.Api.Tests;

public class ServiceControllerTests : IClassFixture<DaemonApiFactory>
{
    private readonly HttpClient _client;

    public ServiceControllerTests(DaemonApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetService_Returns200()
    {
        var response = await _client.GetAsync("/api/v1/service");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetService_ReturnsOkTrue()
    {
        var response = await _client.GetAsync("/api/v1/service");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task GetService_ReturnsRunningStatus()
    {
        var response = await _client.GetAsync("/api/v1/service");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Running", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetService_ReturnsSurfaces()
    {
        var response = await _client.GetAsync("/api/v1/service");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var surfaces = body.GetProperty("surfaces");
        Assert.Equal(JsonValueKind.Array, surfaces.ValueKind);
        Assert.True(surfaces.GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetService_SurfacesIncludeHealthAndCommands()
    {
        var response = await _client.GetAsync("/api/v1/service");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var names = body.GetProperty("surfaces")
            .EnumerateArray()
            .Select(s => s.GetProperty("name").GetString())
            .ToHashSet();

        Assert.Contains("health", names);
        Assert.Contains("commands", names);
    }

    [Fact]
    public async Task PutHostProfile_Returns200()
    {
        var profile = new { hostName = "test-host", workspacePath = "/tmp/workspace", metadata = new { } };
        var response = await _client.PutAsJsonAsync("/api/v1/service/host-profile", profile);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PutHostProfile_ReturnsOkTrue()
    {
        var profile = new { hostName = "test-host", workspacePath = "/tmp/workspace", metadata = new { } };
        var response = await _client.PutAsJsonAsync("/api/v1/service/host-profile", profile);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task PutHostProfile_EmptyBody_Returns400()
    {
        var response = await _client.PutAsync(
            "/api/v1/service/host-profile",
            new StringContent("", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
