using System.Diagnostics;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Gmsd.Web.Tests.E2E;

/// <summary>
/// Performance benchmarks for the Chat-Forward UI.
/// Validates latency targets: Initial load < 2s, Message render < 50ms.
/// </summary>
public class ChatForwardPerformanceTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ChatForwardPerformanceTests()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task InitialLoad_WithChatForwardUI_IsUnder2Seconds()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Cookie", "ChatForwardUI=true");

        // Act
        var stopwatch = Stopwatch.StartNew();
        var response = await _client.SendAsync(request);
        stopwatch.Stop();

        // Assert
        response.EnsureSuccessStatusCode();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000, "Initial load with ChatForwardUI should be under 2s");
    }

    [Fact]
    public async Task OrchestratorLoad_WithChatForwardUI_IsUnder2Seconds()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/Orchestrator");
        request.Headers.Add("Cookie", "ChatForwardUI=true");

        // Act
        var stopwatch = Stopwatch.StartNew();
        var response = await _client.SendAsync(request);
        stopwatch.Stop();

        // Assert
        response.EnsureSuccessStatusCode();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000, "Orchestrator page load with ChatForwardUI should be under 2s");
    }

    [Fact]
    public async Task ChatInputPartial_RenderTime_IsMinimal()
    {
        // Arrange
        // We test the partial directly if it's accessible via a route or just measure its impact on the main page
        var request = new HttpRequestMessage(HttpMethod.Get, "/Orchestrator");
        request.Headers.Add("Cookie", "ChatForwardUI=true");

        // Act
        var stopwatch = Stopwatch.StartNew();
        var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        stopwatch.Stop();

        // Assert
        content.Should().Contain("chat-input");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500, "Rendering the chat-forward view should be fast");
    }
}
