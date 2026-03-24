using System.Net;
using System.Net.Http.Json;
using System.IO;
using System.Text.Json;
using Xunit;

namespace nirmata.Windows.Service.Api.Tests;

public class CommandsControllerTests : IClassFixture<DaemonApiFactory>
{
    private readonly HttpClient _client;

    public CommandsControllerTests(DaemonApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostCommands_EmptyArgv_Returns400()
    {
        var request = new { argv = Array.Empty<string>() };
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostCommands_EmptyArgv_ReturnsOkFalseWithMessage()
    {
        var request = new { argv = Array.Empty<string>() };
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(body.GetProperty("ok").GetBoolean());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("output").GetString()));
    }

    [Fact]
    public async Task PostCommands_MissingArgv_Returns400()
    {
        // argv is required — missing property should be rejected by model binding
        var response = await _client.PostAsJsonAsync("/api/v1/commands", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostCommands_ValidArgv_Returns200WithOkAndOutput()
    {
        // dotnet is always available in the test environment
        var request = new { argv = new[] { "dotnet", "--version" } };
        var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("ok", out var ok));
        Assert.True(body.TryGetProperty("output", out _));
        Assert.True(ok.GetBoolean());
    }

    [Fact]
    public async Task PostCommands_AosInit_ResolvesLocalCliAndUsesExplicitWorkingDirectory()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"daemon-aos-init-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDirectory);

        try
        {
            var request = new { argv = new[] { "aos", "init" }, workingDirectory };
            var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.GetProperty("ok").GetBoolean(), body.GetProperty("output").GetString());
            Assert.True(Directory.Exists(Path.Combine(workingDirectory, ".aos")), "Expected '.aos' to be created by init.");
            Assert.Contains(workingDirectory, body.GetProperty("output").GetString() ?? string.Empty);
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PostCommands_WithExplicitWorkingDirectory_ExecutesInThatDirectory()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"daemon-cwd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDirectory);

        try
        {
            var request = new { argv = new[] { "cmd", "/c", "cd" }, workingDirectory };
            var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.GetProperty("ok").GetBoolean());
            Assert.Equal(workingDirectory, body.GetProperty("output").GetString()?.Trim());
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }
}
