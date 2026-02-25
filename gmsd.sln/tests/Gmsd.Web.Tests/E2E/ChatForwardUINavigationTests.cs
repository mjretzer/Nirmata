using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using System.Net;

namespace Gmsd.Web.Tests.E2E;

/// <summary>
/// E2E tests for the new Chat-Forward UI components and navigation.
/// </summary>
public class ChatForwardUINavigationTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ChatForwardUINavigationTests()
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
    public async Task ChatForwardUI_WhenEnabled_RendersMainLayout()
    {
        // Arrange
        // We need to enable the feature flag. Since it's stored in preferences/config,
        // we might need to set a cookie or use the API to enable it for the session.
        // For now, let's assume we can trigger it via a cookie if implemented, 
        // or we check if the elements exist when we force the layout.
        
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        // Simulate opting in via cookie if the implementation supports it
        request.Headers.Add("Cookie", "ChatForwardUI=true"); 
        
        var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        
        // Check for core layout elements defined in T1
        content.Should().Contain("main-layout", "Main layout container should be present");
        content.Should().Contain("sidebar", "Context sidebar should be present");
        content.Should().Contain("chat-area", "Chat area should be present");
        content.Should().Contain("detail-panel", "Detail panel should be present");
    }

    [Fact]
    public async Task ChatThread_RendersCorrectly()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/Orchestrator");
        request.Headers.Add("Cookie", "ChatForwardUI=true");

        // Act
        var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().Contain("message-thread", "Message thread container should be present");
        content.Should().Contain("chat-input", "Chat input should be present");
    }

    [Fact]
    public async Task DetailPanel_IsPresent_OnEntityPages()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/Projects");
        request.Headers.Add("Cookie", "ChatForwardUI=true");

        // Act
        var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().Contain("detail-panel", "Detail panel should be present on Projects page");
    }

    [Fact]
    public async Task Accessibility_SkipLinks_ArePresent()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Cookie", "ChatForwardUI=true");

        // Act
        var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().Contain("skip-to-main", "Skip to main content link should be present");
        content.Should().Contain("skip-to-chat", "Skip to chat input link should be present");
    }
}
