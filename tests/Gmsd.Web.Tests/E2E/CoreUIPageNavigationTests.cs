using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Gmsd.Web.Tests.E2E;

/// <summary>
/// E2E tests for core UI page navigation flows.
/// These tests verify that all pages render correctly and navigation between them works.
/// </summary>
public class CoreUIPageNavigationTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory;

    public CoreUIPageNavigationTests()
    {
        _factory = new TestWebApplicationFactory();
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Workspace")]
    [InlineData("/Dashboard")]
    [InlineData("/Projects")]
    [InlineData("/Runs")]
    [InlineData("/Orchestrator")]
    [InlineData("/Specs")]
    [InlineData("/Roadmap")]
    [InlineData("/Milestones")]
    [InlineData("/Phases")]
    [InlineData("/Tasks")]
    [InlineData("/Uat")]
    [InlineData("/Issues")]
    public async Task Get_Page_ReturnsSuccessStatusCode(string url)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }

    [Fact]
    public async Task Navigation_Links_ArePresent_OnAllPages()
    {
        // Arrange
        var client = _factory.CreateClient();
        var pages = new[] { "/", "/Workspace", "/Dashboard", "/Projects", "/Runs", "/Orchestrator", "/Specs",
                            "/Roadmap", "/Milestones", "/Phases", "/Tasks", "/Uat", "/Issues" };

        foreach (var page in pages)
        {
            // Act
            var response = await client.GetAsync(page);
            var content = await response.Content.ReadAsStringAsync();

            // Assert - Check that navigation links exist
            content.Should().Contain("href=\"/\"", $"Home link should exist on {page}");
            content.Should().Contain("href=\"/Workspace\"", $"Workspace link should exist on {page}");
            content.Should().Contain("href=\"/Projects\"", $"Projects link should exist on {page}");
            content.Should().Contain("href=\"/Runs\"", $"Runs link should exist on {page}");
            content.Should().Contain("href=\"/Orchestrator\"", $"Orchestrator link should exist on {page}");
            content.Should().Contain("href=\"/Specs\"", $"Specs link should exist on {page}");
            content.Should().Contain("href=\"/Roadmap\"", $"Roadmap link should exist on {page}");
            content.Should().Contain("href=\"/Milestones\"", $"Milestones link should exist on {page}");
            content.Should().Contain("href=\"/Phases\"", $"Phases link should exist on {page}");
            content.Should().Contain("href=\"/Tasks\"", $"Tasks link should exist on {page}");
            content.Should().Contain("href=\"/Uat\"", $"UAT link should exist on {page}");
            content.Should().Contain("href=\"/Issues\"", $"Issues link should exist on {page}");
        }
    }

    [Theory]
    [InlineData("/", "GMSD")]
    [InlineData("/Workspace", "Workspace")]
    [InlineData("/Dashboard", "Dashboard")]
    [InlineData("/Orchestrator", "Orchestrator")]
    public async Task Get_Page_ContainsExpectedTitle(string url, string expectedTitle)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().Contain($"<title>", $"Page should have a title on {url}");
        content.Should().Contain(expectedTitle, $"Page title should contain '{expectedTitle}' on {url}");
    }

    [Fact]
    public async Task WorkspacePage_ContainsExpectedFormElements()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Workspace");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().Contain("workspace-path", "Workspace page should have path input");
        content.Should().Contain("htmx", "Workspace page should use HTMX");
    }

    [Fact]
    public async Task OrchestratorPage_ContainsChatInterface()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Orchestrator");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().Contain("orchestrator-input", "Orchestrator page should have command input");
        content.Should().Contain("message-thread", "Orchestrator page should have message thread");
    }

    [Fact]
    public async Task SpecsPage_ContainsTreeViewStructure()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Specs");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().Contain("spec-tree", "Specs page should have tree view");
        content.Should().Contain("spec-search", "Specs page should have search");
    }

    [Fact]
    public async Task AllPages_ContainHTMXReference()
    {
        // Arrange
        var client = _factory.CreateClient();
        var pages = new[] { "/", "/Workspace", "/Dashboard", "/Projects", "/Runs", "/Orchestrator", "/Specs",
                            "/Roadmap", "/Milestones", "/Phases", "/Tasks", "/Uat", "/Issues" };

        foreach (var page in pages)
        {
            // Act
            var response = await client.GetAsync(page);
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            content.Should().Contain("htmx.org", $"Page {page} should reference HTMX library");
        }
    }

    [Fact]
    public async Task AllPages_ContainPrismJSReference()
    {
        // Arrange
        var client = _factory.CreateClient();
        var pages = new[] { "/", "/Workspace", "/Dashboard", "/Projects", "/Runs", "/Orchestrator", "/Specs",
                            "/Roadmap", "/Milestones", "/Phases", "/Tasks", "/Uat", "/Issues" };

        foreach (var page in pages)
        {
            // Act
            var response = await client.GetAsync(page);
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            content.Should().Contain("prism", $"Page {page} should reference Prism.js for syntax highlighting");
        }
    }

    [Theory]
    [InlineData("/Roadmap", "Roadmap")]
    [InlineData("/Milestones", "Milestone")]
    [InlineData("/Phases", "Phase")]
    [InlineData("/Tasks", "Task")]
    [InlineData("/Uat", "UAT")]
    [InlineData("/Issues", "Issue")]
    public async Task Get_NewPages_ContainsExpectedTitle(string url, string expectedTitle)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().Contain($"<title>", $"Page should have a title on {url}");
        content.Should().Contain(expectedTitle, $"Page title should contain '{expectedTitle}' on {url}");
    }

    [Fact]
    public async Task RoadmapPage_ContainsTimelineStructure()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Roadmap");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().Contain("roadmap-controls", "Roadmap page should have phase controls");
        content.Should().Contain("milestone-timeline", "Roadmap page should have milestone timeline");
    }

    [Fact]
    public async Task MilestonesPage_ContainsListStructure()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Milestones");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().Contain("table", "Milestones page should have a table");
    }

    [Fact]
    public async Task PhasesPage_ContainsListStructure()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Phases");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().Contain("table", "Phases page should have a table");
    }

    [Fact]
    public async Task TasksPage_ContainsFilterControls()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Tasks");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().Contain("filter-select", "Tasks page should have filter controls");
    }

    [Fact]
    public async Task UatPage_ContainsListStructure()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Uat");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().Contain("table", "UAT page should have a table");
    }

    [Fact]
    public async Task IssuesPage_ContainsFilterControls()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Issues");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().Contain("filter-select", "Issues page should have filter controls");
    }

    [Fact]
    public async Task LegacyCommandUrl_RedirectsToOrchestrator()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/Command");

        // Assert
        response.StatusCode.Should().BeOneOf(
            System.Net.HttpStatusCode.MovedPermanently,  // 301
            System.Net.HttpStatusCode.Redirect,          // 302
            System.Net.HttpStatusCode.SeeOther           // 303
        );
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("Orchestrator");
    }

    [Fact]
    public async Task LegacyCommandUrl_WithAutoRedirect_LoadsOrchestratorPage()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true
        });

        // Act
        var response = await client.GetAsync("/Command");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        content.Should().Contain("<title>", "Page should have a title");
        content.Should().Contain("Orchestrator", "Page title should contain 'Orchestrator'");
    }
}
