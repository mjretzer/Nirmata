using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace nirmata.Windows.Service.Api.Tests;

public class DiagnosticsControllerTests : IClassFixture<DaemonApiFactory>
{
    private readonly HttpClient _client;

    public DiagnosticsControllerTests(DaemonApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetDiagnostics_Returns200()
    {
        var response = await _client.GetAsync("/api/v1/diagnostics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetDiagnostics_NoWorkspace_ReturnsEmptyCollections()
    {
        var response = await _client.GetAsync("/api/v1/diagnostics");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // No workspace is registered in the default state
        Assert.Equal(JsonValueKind.Array, body.GetProperty("logs").ValueKind);
        Assert.Equal(JsonValueKind.Array, body.GetProperty("artifacts").ValueKind);
        Assert.Equal(JsonValueKind.Array, body.GetProperty("locks").ValueKind);
        Assert.Equal(JsonValueKind.Array, body.GetProperty("cacheEntries").ValueKind);

        Assert.Equal(0, body.GetProperty("logs").GetArrayLength());
        Assert.Equal(0, body.GetProperty("artifacts").GetArrayLength());
        Assert.Equal(0, body.GetProperty("locks").GetArrayLength());
        Assert.Equal(0, body.GetProperty("cacheEntries").GetArrayLength());
    }

    [Fact]
    public async Task DeleteDiagnosticsLocks_Returns200()
    {
        var response = await _client.DeleteAsync("/api/v1/diagnostics/locks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteDiagnosticsLocks_NoWorkspace_ReturnsZeroRemoved()
    {
        var response = await _client.DeleteAsync("/api/v1/diagnostics/locks");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(0, body.GetProperty("removed").GetInt32());
    }

    [Fact]
    public async Task DeleteDiagnosticsCache_Returns200()
    {
        var response = await _client.DeleteAsync("/api/v1/diagnostics/cache");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteDiagnosticsCache_NoWorkspace_ReturnsZeroRemoved()
    {
        var response = await _client.DeleteAsync("/api/v1/diagnostics/cache");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(0, body.GetProperty("removed").GetInt32());
    }
}
