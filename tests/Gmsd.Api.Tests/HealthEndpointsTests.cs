using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Gmsd.Api.Tests;

public class HealthEndpointsTests : IClassFixture<GmsdApiFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointsTests(GmsdApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_Returns200Ok()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetHealth_ReturnsHealthyStatus()
    {
        var response = await _client.GetAsync("/health");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("status", out var statusElement));
        Assert.Equal("Healthy", statusElement.GetString());
    }

    [Fact]
    public async Task GetApiHealth_ReturnsDetailedHealthJson()
    {
        var response = await _client.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("status", out _));
        Assert.True(root.TryGetProperty("timestamp", out _));
        Assert.True(root.TryGetProperty("totalDurationMs", out _));
        Assert.True(root.TryGetProperty("dependencies", out _));
    }

    [Fact]
    public async Task GetApiHealth_ReturnsHealthyStatus()
    {
        var response = await _client.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("status", out var statusElement));
        Assert.Equal("Healthy", statusElement.GetString());
    }

    [Fact]
    public async Task GetApiHealth_IncludesDatabaseStatus()
    {
        var response = await _client.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("dependencies", out var dependenciesElement));
        Assert.True(dependenciesElement.TryGetProperty("database", out var databaseElement));

        Assert.True(databaseElement.TryGetProperty("status", out var dbStatusElement));
        Assert.Equal("Healthy", dbStatusElement.GetString());

        Assert.True(databaseElement.TryGetProperty("durationMs", out _));
    }

    [Fact]
    public async Task GetApiHealth_DatabaseHasNoError()
    {
        var response = await _client.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("dependencies", out var dependenciesElement));
        Assert.True(dependenciesElement.TryGetProperty("database", out var databaseElement));

        // Error property should either not exist or be null
        if (databaseElement.TryGetProperty("error", out var errorElement))
        {
            Assert.True(errorElement.ValueKind == JsonValueKind.Null || string.IsNullOrEmpty(errorElement.GetString()));
        }
    }
}
