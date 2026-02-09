using FluentAssertions;
using Xunit;

namespace Gmsd.Web.Tests.E2E;

/// <summary>
/// E2E tests for shared UI components (RunCard, JsonViewer, ValidationReport).
/// </summary>
public class SharedComponentsTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory;

    public SharedComponentsTests()
    {
        _factory = new TestWebApplicationFactory();
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    [Fact]
    public async Task RunCard_Component_RendersWithCorrectStructure()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - RunCard is typically shown on Dashboard or Runs pages
        var response = await client.GetAsync("/Runs");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - Check for RunCard structure
        content.Should().Contain("run-card", "RunCard component should have correct CSS class");
        content.Should().Contain("run-card-header", "RunCard should have header section");
        content.Should().Contain("run-card-body", "RunCard should have body section");
        content.Should().Contain("run-status-badge", "RunCard should show status badge");
    }

    [Theory]
    [InlineData("success", "run-success")]
    [InlineData("failed", "run-failed")]
    [InlineData("running", "run-running")]
    public async Task RunCard_DisplaysCorrectStatusClass(string status, string expectedClass)
    {
        // This test would require a page that displays runs with specific statuses
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Runs");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - The CSS classes should be defined in the stylesheet
        content.Should().Contain(expectedClass, $"RunCard should support {status} status with class {expectedClass}");
    }

    [Fact]
    public async Task JsonViewer_Component_ContainsSyntaxHighlightingElements()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - JsonViewer is used on Specs/View page
        var response = await client.GetAsync("/Specs/View?path=project.json");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().Contain("json-viewer", "JsonViewer should have correct CSS class");
        content.Should().Contain("language-json", "JsonViewer should use Prism language-json class");
        content.Should().Contain("<pre", "JsonViewer should use pre tag for formatting");
        content.Should().Contain("<code", "JsonViewer should use code tag for content");
    }

    [Fact]
    public async Task JsonViewer_ContainsToolbarActions()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Specs/View?path=project.json");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().Contain("json-viewer-toolbar", "JsonViewer should have toolbar");
        content.Should().Contain("copyToClipboard", "JsonViewer should have copy functionality");
        content.Should().Contain("toggleFormat", "JsonViewer should have format toggle");
    }

    [Fact]
    public async Task ValidationReport_Component_ShowsValidationStatus()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - ValidationReport is shown when validation errors exist
        var response = await client.GetAsync("/Specs/Project");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - Check for ValidationReport structure (may not always be visible)
        content.Should().Contain("validation-report", "ValidationReport component CSS class should exist");
    }

    [Fact]
    public async Task ValidationReport_DisplaysErrorsWhenPresent()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - Try to view project spec which should have validation
        var response = await client.GetAsync("/Specs/Project");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().Contain("validation-success", "ValidationReport should have success state class");
        content.Should().Contain("validation-error", "ValidationReport should have error state class");
    }

    [Fact]
    public async Task AllPages_ContainSharedLayoutComponents()
    {
        // Arrange
        var client = _factory.CreateClient();
        var pages = new[] { "/", "/Workspace", "/Dashboard", "/Projects", "/Runs", "/Command", "/Specs" };

        foreach (var page in pages)
        {
            // Act
            var response = await client.GetAsync(page);
            var content = await response.Content.ReadAsStringAsync();

            // Assert - Layout components
            content.Should().Contain("navbar", $"Page {page} should have navigation bar");
            content.Should().Contain("footer", $"Page {page} should have footer");
            content.Should().Contain("container", $"Page {page} should use container layout");
        }
    }

    [Fact]
    public async Task Navigation_ActiveState_IsSetForCurrentPage()
    {
        // Arrange
        var client = _factory.CreateClient();
        var pages = new[] { "/", "/Workspace", "/Projects", "/Runs", "/Command", "/Specs" };

        foreach (var page in pages)
        {
            // Act
            var response = await client.GetAsync(page);
            var content = await response.Content.ReadAsStringAsync();

            // Assert - Active state styling
            content.Should().Contain("active", $"Page {page} should highlight active navigation item");
        }
    }

    [Theory]
    [InlineData("/", "Home")]
    [InlineData("/Workspace", "Workspace")]
    [InlineData("/Command", "Command")]
    [InlineData("/Specs", "Specs")]
    public async Task PageTitles_AreDescriptive(string url, string expectedText)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().Contain($"<h1", $"Page {url} should have a heading");
        content.Should().Contain(expectedText, $"Page {url} should contain '{expectedText}'");
    }
}
