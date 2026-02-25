using FluentAssertions;
using Xunit;

namespace Gmsd.Web.Tests.E2E;

/// <summary>
/// E2E tests for critical user flows across the execution & verification UI.
/// Tests the workflow: Roadmap → Milestones → Phases → Tasks → UAT → Issues
/// </summary>
public class ExecutionVerificationFlowTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory;

    public ExecutionVerificationFlowTests()
    {
        _factory = new TestWebApplicationFactory();
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    [Fact]
    public async Task Flow_RoadmapToMilestone_NavigationWorks()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - Start at Roadmap
        var response = await client.GetAsync("/Roadmap");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - Should have links to Milestones
        response.EnsureSuccessStatusCode();
        content.Should().Contain("href=\"/Milestones", "Roadmap should link to Milestones");
    }

    [Fact]
    public async Task Flow_MilestonesToPhases_NavigationWorks()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Milestones");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - Should have links to Phases
        response.EnsureSuccessStatusCode();
        content.Should().Contain("href=\"/Phases", "Milestones should link to Phases");
    }

    [Fact]
    public async Task Flow_PhasesToTasks_NavigationWorks()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Phases");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - Should have links to Tasks
        response.EnsureSuccessStatusCode();
        content.Should().Contain("href=\"/Tasks", "Phases should link to Tasks");
    }

    [Fact]
    public async Task Flow_TasksToUat_NavigationWorks()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Tasks");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - Should have links to UAT
        response.EnsureSuccessStatusCode();
        content.Should().Contain("href=\"/Uat", "Tasks should link to UAT");
    }

    [Fact]
    public async Task Flow_UatToIssues_NavigationWorks()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Uat");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - Should have links to Issues
        response.EnsureSuccessStatusCode();
        content.Should().Contain("href=\"/Issues", "UAT should link to Issues");
    }

    [Fact]
    public async Task Flow_IssuesBackToTasks_NavigationWorks()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Issues");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - Should have links back to Tasks
        response.EnsureSuccessStatusCode();
        content.Should().Contain("href=\"/Tasks", "Issues should link back to Tasks");
    }

    [Fact]
    public async Task Flow_AllPages_HaveConsistentNavigation()
    {
        // Arrange
        var client = _factory.CreateClient();
        var flowPages = new[] { "/Roadmap", "/Milestones", "/Phases", "/Tasks", "/Uat", "/Issues" };

        foreach (var page in flowPages)
        {
            // Act
            var response = await client.GetAsync(page);
            var content = await response.Content.ReadAsStringAsync();

            // Assert - All pages in the flow should have consistent nav
            response.EnsureSuccessStatusCode();
            content.Should().Contain("navbar", $"{page} should have navigation bar");
            content.Should().Contain("href=\"/Roadmap", $"{page} should have Roadmap link");
            content.Should().Contain("href=\"/Milestones", $"{page} should have Milestones link");
            content.Should().Contain("href=\"/Phases", $"{page} should have Phases link");
            content.Should().Contain("href=\"/Tasks", $"{page} should have Tasks link");
            content.Should().Contain("href=\"/Uat", $"{page} should have UAT link");
            content.Should().Contain("href=\"/Issues", $"{page} should have Issues link");
        }
    }

    [Fact]
    public async Task Flow_RunsPage_LinkedFromAllExecutionPages()
    {
        // Arrange
        var client = _factory.CreateClient();
        var executionPages = new[] { "/Tasks", "/Uat", "/Issues" };

        foreach (var page in executionPages)
        {
            // Act
            var response = await client.GetAsync(page);
            var content = await response.Content.ReadAsStringAsync();

            // Assert - Execution pages should link to Runs
            response.EnsureSuccessStatusCode();
            content.Should().Contain("href=\"/Runs", $"{page} should have link to Runs page");
        }
    }

    [Fact]
    public async Task Flow_SpecsPage_LinkedFromAllExecutionPages()
    {
        // Arrange
        var client = _factory.CreateClient();
        var executionPages = new[] { "/Roadmap", "/Milestones", "/Phases", "/Tasks", "/Uat", "/Issues" };

        foreach (var page in executionPages)
        {
            // Act
            var response = await client.GetAsync(page);
            var content = await response.Content.ReadAsStringAsync();

            // Assert - All execution pages should have link to Specs
            response.EnsureSuccessStatusCode();
            content.Should().Contain("href=\"/Specs", $"{page} should have link to Specs page");
        }
    }

    [Fact]
    public async Task Flow_Roadmap_ContainsPhaseControls()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Roadmap");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        content.Should().Contain("roadmap-controls", "Roadmap should have phase controls");
        content.Should().Contain("Add Phase", "Roadmap should have Add Phase button");
    }

    [Fact]
    public async Task Flow_PhasesDetail_HasTabbedInterface()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - Try to access a phase detail page (may not have data but should render)
        var response = await client.GetAsync("/Phases/Details/test-phase");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        content.Should().Contain("tabs-container", "Phase details should have tabs");
        content.Should().Contain("tab-button", "Phase details should have tab buttons");
    }

    [Fact]
    public async Task Flow_TasksDetail_HasJsonTabs()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Tasks/Details/test-task");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        content.Should().Contain("tabs-container", "Task details should have tabs");
        content.Should().Contain("task.json", "Task details should reference task.json");
        content.Should().Contain("plan.json", "Task details should reference plan.json");
        content.Should().Contain("uat.json", "Task details should reference uat.json");
    }

    [Fact]
    public async Task Flow_UatVerify_HasWizardStructure()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Uat/Verify");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        content.Should().Contain("wizard-checklist", "UAT Verify should have wizard checklist");
        content.Should().Contain("wizard-actions", "UAT Verify should have wizard actions");
    }

    [Fact]
    public async Task Flow_IssuesDetail_HasResolutionActions()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Issues/Details/test-issue");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        content.Should().Contain("detail-page", "Issue details should use detail page layout");
        content.Should().Contain("section-card", "Issue details should have sections");
    }

    [Fact]
    public async Task Flow_AllNewPages_RenderSuccessfully()
    {
        // Arrange
        var client = _factory.CreateClient();
        var newPages = new[] { "/Roadmap", "/Milestones", "/Phases", "/Tasks", "/Uat", "/Issues" };

        // Act & Assert
        foreach (var page in newPages)
        {
            var response = await client.GetAsync(page);
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, 
                $"{page} should render successfully (200 OK)");
            response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        }
    }

    [Fact]
    public async Task Flow_AllDetailPages_RenderSuccessfully()
    {
        // Arrange
        var client = _factory.CreateClient();
        var detailPages = new[] { "/Milestones/Details/test", "/Phases/Details/test", "/Tasks/Details/test", "/Issues/Details/test" };

        // Act & Assert - Detail pages should render (even with missing data)
        foreach (var page in detailPages)
        {
            var response = await client.GetAsync(page);
            // Should get 200 even if data not found (page handles gracefully)
            response.StatusCode.Should().BeOneOf(
                System.Net.HttpStatusCode.OK, 
                System.Net.HttpStatusCode.NotFound);
        }
    }
}
