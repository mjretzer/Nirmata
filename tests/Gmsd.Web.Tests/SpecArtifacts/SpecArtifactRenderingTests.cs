using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Gmsd.Web.Tests.SpecArtifacts;

/// <summary>
/// Tests to verify all pages render spec artifacts correctly from the .aos/spec/ directory.
/// </summary>
public class SpecArtifactRenderingTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _aosDir;
    private readonly string _specDir;
    private readonly string _stateDir;

    public SpecArtifactRenderingTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _aosDir = Path.Combine(_tempDir, ".aos");
        _specDir = Path.Combine(_aosDir, "spec");
        _stateDir = Path.Combine(_aosDir, "state");
        Directory.CreateDirectory(_specDir);
        Directory.CreateDirectory(_stateDir);
        Directory.CreateDirectory(Path.Combine(_specDir, "tasks"));
        Directory.CreateDirectory(Path.Combine(_specDir, "issues"));
        Directory.CreateDirectory(Path.Combine(_specDir, "phases"));
        Directory.CreateDirectory(Path.Combine(_specDir, "milestones"));
        Directory.CreateDirectory(Path.Combine(_specDir, "uat"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    private void CreateWorkspaceConfig()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var configDir = Path.Combine(appData, "Gmsd");
        Directory.CreateDirectory(configDir);
        var configFile = Path.Combine(configDir, "workspace-config.json");
        var json = $"{{\"SelectedWorkspacePath\": \"{_tempDir.Replace("\\", "\\\\")}\"}}";
        File.WriteAllText(configFile, json);
    }

    private void CreateRoadmapSpec()
    {
        var roadmapFile = Path.Combine(_specDir, "roadmap.json");
        var json = @"{
            ""schemaVersion"": 1,
            ""roadmap"": {
                ""title"": ""Project Roadmap"",
                ""items"": [
                    {
                        ""id"": ""MS-001"",
                        ""title"": ""MVP Milestone"",
                        ""kind"": ""milestone"",
                        ""description"": ""Minimum viable product"",
                        ""targetDate"": ""2026-03-01""
                    },
                    {
                        ""id"": ""PH-001"",
                        ""title"": ""Foundation Phase"",
                        ""kind"": ""phase"",
                        ""milestoneId"": ""MS-001"",
                        ""description"": ""Set up project foundation"",
                        ""goals"": [""Setup CI/CD"", ""Configure database""],
                        ""outcomes"": [""Working pipeline"", ""Database schema""]
                    },
                    {
                        ""id"": ""PH-002"",
                        ""title"": ""Core Features Phase"",
                        ""kind"": ""phase"",
                        ""milestoneId"": ""MS-001"",
                        ""description"": ""Implement core features"",
                        ""goals"": [""User auth"", ""API endpoints""],
                        ""outcomes"": [""Login working"", ""REST API""]
                    }
                ]
            }
        }";
        File.WriteAllText(roadmapFile, json);
    }

    private void CreateTasksSpec()
    {
        var tasksDir = Path.Combine(_specDir, "tasks");
        Directory.CreateDirectory(tasksDir);
        
        // tasks.json
        var tasksFile = Path.Combine(tasksDir, "tasks.json");
        var tasksJson = @"{
            ""schemaVersion"": 1,
            ""tasks"": [
                {
                    ""id"": ""TASK-001"",
                    ""title"": ""Setup CI/CD Pipeline"",
                    ""description"": ""Configure GitHub Actions for CI/CD"",
                    ""phaseId"": ""PH-001"",
                    ""status"": ""Completed"",
                    ""acceptanceCriteria"": [
                        ""CI pipeline runs on PR"",
                        ""CD deploys to staging"",
                        ""Tests pass in CI""
                    ]
                },
                {
                    ""id"": ""TASK-002"",
                    ""title"": ""Create Database Schema"",
                    ""description"": ""Design and implement database schema"",
                    ""phaseId"": ""PH-001"",
                    ""status"": ""InProgress"",
                    ""acceptanceCriteria"": [
                        ""Schema migration created"",
                        ""Seed data added"",
                        ""Tests pass""
                    ]
                }
            ]
        }";
        File.WriteAllText(tasksFile, tasksJson);

        // Individual task files
        var taskDir = Path.Combine(tasksDir, "TASK-001");
        Directory.CreateDirectory(taskDir);
        File.WriteAllText(Path.Combine(taskDir, "task.json"), @"{
            ""id"": ""TASK-001"",
            ""title"": ""Setup CI/CD Pipeline"",
            ""description"": ""Configure GitHub Actions for CI/CD""
        }");
        File.WriteAllText(Path.Combine(taskDir, "plan.json"), @"{
            ""steps"": [
                {""order"": 1, ""description"": ""Create workflow file""},
                {""order"": 2, ""description"": ""Configure build job""},
                {""order"": 3, ""description"": ""Configure deploy job""}
            ]
        }");
        File.WriteAllText(Path.Combine(taskDir, "uat.json"), @"{
            ""verificationSteps"": [
                ""Verify build succeeds"",
                ""Verify tests pass"",
                ""Verify deployment works""
            ]
        }");
        File.WriteAllText(Path.Combine(taskDir, "links.json"), @"{
            ""related"": [
                {""type"": ""phase"", ""id"": ""PH-001""}
            ]
        }");
    }

    private void CreateIssuesSpec()
    {
        var issuesDir = Path.Combine(_specDir, "issues");
        Directory.CreateDirectory(issuesDir);
        
        var issuesFile = Path.Combine(issuesDir, "issues.json");
        var json = @"{
            ""schemaVersion"": 1,
            ""issues"": [
                {
                    ""id"": ""ISSUE-001"",
                    ""title"": ""Build fails on Windows"",
                    ""description"": ""CI build fails when running on Windows runner"",
                    ""status"": ""Open"",
                    ""severity"": ""High"",
                    ""type"": ""Bug"",
                    ""taskId"": ""TASK-001"",
                    ""reproSteps"": ""1. Push to PR
2. Windows runner starts
3. Build step fails"",
                    ""expectedBehavior"": ""Build should succeed on all platforms"",
                    ""actualBehavior"": ""Build fails with path separator error""
                },
                {
                    ""id"": ""ISSUE-002"",
                    ""title"": ""Slow database queries"",
                    ""description"": ""Database queries are taking too long"",
                    ""status"": ""InProgress"",
                    ""severity"": ""Medium"",
                    ""type"": ""Performance"",
                    ""taskId"": ""TASK-002""
                }
            ]
        }";
        File.WriteAllText(issuesFile, json);
    }

    private void CreateUatSpec()
    {
        var uatDir = Path.Combine(_specDir, "uat");
        Directory.CreateDirectory(uatDir);
        
        var uatFile = Path.Combine(uatDir, "sessions.json");
        var json = @"{
            ""schemaVersion"": 1,
            ""sessions"": [
                {
                    ""id"": ""UAT-001"",
                    ""taskId"": ""TASK-001"",
                    ""startedAt"": ""2026-02-07T10:00:00Z"",
                    ""completedAt"": ""2026-02-07T11:30:00Z"",
                    ""status"": ""Passed"",
                    ""passedCount"": 3,
                    ""failedCount"": 0,
                    ""skippedCount"": 0
                }
            ]
        }";
        File.WriteAllText(uatFile, json);
    }

    private void CreateStateSpec()
    {
        var stateFile = Path.Combine(_stateDir, "state.json");
        var json = @"{
            ""cursor"": {
                ""phaseId"": ""PH-001"",
                ""milestoneId"": ""MS-001""
            },
            ""phases"": {
                ""PH-001"": {
                    ""status"": ""InProgress"",
                    ""assumptions"": [""CI/CD provider available"", ""Test environment ready""],
                    ""research"": [
                        {
                            ""topic"": ""GitHub Actions pricing"",
                            ""findings"": ""Free for public repos"",
                            ""isComplete"": true
                        }
                    ]
                },
                ""PH-002"": {
                    ""status"": ""Planned"",
                    ""constraints"": [
                        {
                            ""type"": ""Dependency"",
                            ""description"": ""Must complete PH-001 first"",
                            ""isBlocking"": true
                        }
                    ]
                }
            },
            ""milestones"": {
                ""MS-001"": {
                    ""status"": ""InProgress"",
                    ""progressPercent"": 50,
                    ""completedTasks"": 1,
                    ""totalTasks"": 2
                }
            }
        }";
        File.WriteAllText(stateFile, json);
    }

    [Fact]
    public void RoadmapPage_RendersMilestones_FromSpec()
    {
        // Arrange
        CreateWorkspaceConfig();
        CreateRoadmapSpec();
        CreateStateSpec();
        
        var configuration = new ConfigurationBuilder().Build();
        var model = new Gmsd.Web.Pages.Roadmap.IndexModel(
            NullLogger<Gmsd.Web.Pages.Roadmap.IndexModel>.Instance,
            configuration);

        // Act
        model.OnGet();

        // Assert
        model.Roadmap.Milestones.Should().HaveCount(1);
        model.Roadmap.Milestones.First().Id.Should().Be("MS-001");
        model.Roadmap.Milestones.First().Name.Should().Be("MVP Milestone");
        model.Roadmap.Milestones.First().Phases.Should().HaveCount(2);
    }

    [Fact]
    public void RoadmapPage_RendersCurrentPhase_FromState()
    {
        // Arrange
        CreateWorkspaceConfig();
        CreateRoadmapSpec();
        CreateStateSpec();
        
        var configuration = new ConfigurationBuilder().Build();
        var model = new Gmsd.Web.Pages.Roadmap.IndexModel(
            NullLogger<Gmsd.Web.Pages.Roadmap.IndexModel>.Instance,
            configuration);

        // Act
        model.OnGet();

        // Assert
        model.Roadmap.CurrentPhaseId.Should().Be("PH-001");
        model.Roadmap.CurrentMilestoneId.Should().Be("MS-001");
    }

    [Fact]
    public void RoadmapPage_RendersAlignmentWarnings_WhenStateMismatch()
    {
        // Arrange
        CreateWorkspaceConfig();
        CreateRoadmapSpec();
        
        // Create state with non-existent phase
        var stateFile = Path.Combine(_stateDir, "state.json");
        File.WriteAllText(stateFile, @"{""cursor"": {""phaseId"": ""PH-999"", ""milestoneId"": ""MS-999""}}");
        
        var configuration = new ConfigurationBuilder().Build();
        var model = new Gmsd.Web.Pages.Roadmap.IndexModel(
            NullLogger<Gmsd.Web.Pages.Roadmap.IndexModel>.Instance,
            configuration);

        // Act
        model.OnGet();

        // Assert
        model.Roadmap.Warnings.Should().Contain(w => w.Type == "Alignment");
    }

    [Fact]
    public void MilestonesPage_RendersMilestones_FromRoadmapSpec()
    {
        // Arrange
        CreateWorkspaceConfig();
        CreateRoadmapSpec();
        CreateStateSpec();
        
        var configuration = new ConfigurationBuilder().Build();
        var model = new Gmsd.Web.Pages.Milestones.IndexModel(
            NullLogger<Gmsd.Web.Pages.Milestones.IndexModel>.Instance,
            configuration);

        // Act
        model.OnGet();

        // Assert
        model.Milestones.Should().HaveCount(1);
        model.Milestones.First().Id.Should().Be("MS-001");
        model.Milestones.First().Name.Should().Be("MVP Milestone");
    }

    [Fact]
    public void MilestonesDetail_RendersMilestoneWithPhases_FromSpec()
    {
        // Arrange
        CreateWorkspaceConfig();
        CreateRoadmapSpec();
        CreateStateSpec();
        
        var configuration = new ConfigurationBuilder().Build();
        var model = new Gmsd.Web.Pages.Milestones.DetailsModel(
            NullLogger<Gmsd.Web.Pages.Milestones.DetailsModel>.Instance,
            configuration);

        // Act
        model.OnGet("MS-001");

        // Assert
        model.Milestone.Should().NotBeNull();
        model.Milestone!.Id.Should().Be("MS-001");
        model.Milestone.Phases.Should().HaveCount(2);
        model.Milestone.Phases.First().Id.Should().Be("PH-001");
    }

    [Fact]
    public void PhasesPage_RendersPhases_FromRoadmapSpec()
    {
        // Arrange
        CreateWorkspaceConfig();
        CreateRoadmapSpec();
        CreateStateSpec();
        
        var configuration = new ConfigurationBuilder().Build();
        var model = new Gmsd.Web.Pages.Phases.IndexModel(
            NullLogger<Gmsd.Web.Pages.Phases.IndexModel>.Instance,
            configuration);

        // Act
        model.OnGet();

        // Assert
        model.Phases.Should().HaveCount(2);
        model.Phases.First().Id.Should().Be("PH-001");
        model.Phases.First().Name.Should().Be("Foundation Phase");
    }

    [Fact]
    public void PhasesDetail_RendersPhaseGoals_FromRoadmapSpec()
    {
        // Arrange
        CreateWorkspaceConfig();
        CreateRoadmapSpec();
        CreateStateSpec();
        
        var configuration = new ConfigurationBuilder().Build();
        var model = new Gmsd.Web.Pages.Phases.DetailsModel(
            NullLogger<Gmsd.Web.Pages.Phases.DetailsModel>.Instance,
            configuration);

        // Act
        model.OnGet("PH-001");

        // Assert
        model.Phase.Should().NotBeNull();
        model.Phase!.Goals.Should().Contain("Setup CI/CD");
        model.Phase.Outcomes.Should().Contain("Working pipeline");
    }

    [Fact]
    public void PhasesDetail_RendersAssumptions_FromStateSpec()
    {
        // Arrange
        CreateWorkspaceConfig();
        CreateRoadmapSpec();
        CreateStateSpec();
        
        var configuration = new ConfigurationBuilder().Build();
        var model = new Gmsd.Web.Pages.Phases.DetailsModel(
            NullLogger<Gmsd.Web.Pages.Phases.DetailsModel>.Instance,
            configuration);

        // Act
        model.OnGet("PH-001");

        // Assert
        model.Phase.Should().NotBeNull();
        model.Phase!.Assumptions.Should().Contain("CI/CD provider available");
        model.Phase.Assumptions.Should().Contain("Test environment ready");
    }

    [Fact]
    public void PhasesDetail_RendersResearch_FromStateSpec()
    {
        // Arrange
        CreateWorkspaceConfig();
        CreateRoadmapSpec();
        CreateStateSpec();
        
        var configuration = new ConfigurationBuilder().Build();
        var model = new Gmsd.Web.Pages.Phases.DetailsModel(
            NullLogger<Gmsd.Web.Pages.Phases.DetailsModel>.Instance,
            configuration);

        // Act
        model.OnGet("PH-001");

        // Assert
        model.Phase.Should().NotBeNull();
        model.Phase!.Research.Should().HaveCount(1);
        model.Phase.Research.First().Topic.Should().Be("GitHub Actions pricing");
    }

    [Fact]
    public void PhasesDetail_RendersConstraints_FromStateSpec()
    {
        // Arrange
        CreateWorkspaceConfig();
        CreateRoadmapSpec();
        CreateStateSpec();
        
        var configuration = new ConfigurationBuilder().Build();
        var model = new Gmsd.Web.Pages.Phases.DetailsModel(
            NullLogger<Gmsd.Web.Pages.Phases.DetailsModel>.Instance,
            configuration);

        // Act
        model.OnGet("PH-002");

        // Assert
        model.Phase.Should().NotBeNull();
        model.Phase!.Constraints.Should().HaveCount(1);
        model.Phase.Constraints.First().Type.Should().Be("Dependency");
        model.Phase.Constraints.First().IsBlocking.Should().BeTrue();
    }

    [Fact]
    public void TasksPage_RendersTasks_FromTasksSpec()
    {
        // Arrange
        CreateWorkspaceConfig();
        CreateTasksSpec();
        
        var configuration = new ConfigurationBuilder().Build();
        var model = new Gmsd.Web.Pages.Tasks.IndexModel(
            NullLogger<Gmsd.Web.Pages.Tasks.IndexModel>.Instance,
            configuration);

        // Act
        model.OnGet();

        // Assert
        model.Tasks.Should().HaveCount(2);
        model.Tasks.First().Id.Should().Be("TASK-001");
        model.Tasks.First().Title.Should().Be("Setup CI/CD Pipeline");
    }

    [Fact]
    public void TasksDetail_RendersAcceptanceCriteria_FromTasksSpec()
    {
        // Arrange
        CreateWorkspaceConfig();
        CreateTasksSpec();
        
        var configuration = new ConfigurationBuilder().Build();
        var model = new Gmsd.Web.Pages.Tasks.DetailsModel(
            NullLogger<Gmsd.Web.Pages.Tasks.DetailsModel>.Instance,
            configuration);

        // Act
        model.OnGet("TASK-001");

        // Assert
        model.Task.Should().NotBeNull();
        model.Task!.AcceptanceCriteria.Should().HaveCount(3);
        model.Task.AcceptanceCriteria.Should().Contain("CI pipeline runs on PR");
    }

    [Fact]
    public void TasksDetail_RendersTaskJson_FromSpecFile()
    {
        // Arrange
        CreateWorkspaceConfig();
        CreateTasksSpec();
        
        var configuration = new ConfigurationBuilder().Build();
        var model = new Gmsd.Web.Pages.Tasks.DetailsModel(
            NullLogger<Gmsd.Web.Pages.Tasks.DetailsModel>.Instance,
            configuration);

        // Act
        model.OnGet("TASK-001");

        // Assert
        model.TaskJson.Should().NotBeNullOrEmpty();
        model.TaskJson.Should().Contain("TASK-001");
        model.TaskJson.Should().Contain("Setup CI/CD Pipeline");
    }

    [Fact]
    public void TasksDetail_RendersPlanJson_FromSpecFile()
    {
        // Arrange
        CreateWorkspaceConfig();
        CreateTasksSpec();
        
        var configuration = new ConfigurationBuilder().Build();
        var model = new Gmsd.Web.Pages.Tasks.DetailsModel(
            NullLogger<Gmsd.Web.Pages.Tasks.DetailsModel>.Instance,
            configuration);

        // Act
        model.OnGet("TASK-001");

        // Assert
        model.PlanJson.Should().NotBeNullOrEmpty();
        model.PlanJson.Should().Contain("steps");
        model.PlanJson.Should().Contain("Create workflow file");
    }

    [Fact]
    public void TasksDetail_RendersUatJson_FromSpecFile()
    {
        // Arrange
        CreateWorkspaceConfig();
        CreateTasksSpec();
        
        var configuration = new ConfigurationBuilder().Build();
        var model = new Gmsd.Web.Pages.Tasks.DetailsModel(
            NullLogger<Gmsd.Web.Pages.Tasks.DetailsModel>.Instance,
            configuration);

        // Act
        model.OnGet("TASK-001");

        // Assert
        model.UatJson.Should().NotBeNullOrEmpty();
        model.UatJson.Should().Contain("verificationSteps");
    }

    [Fact]
    public void TasksDetail_RendersLinksJson_FromSpecFile()
    {
        // Arrange
        CreateWorkspaceConfig();
        CreateTasksSpec();
        
        var configuration = new ConfigurationBuilder().Build();
        var model = new Gmsd.Web.Pages.Tasks.DetailsModel(
            NullLogger<Gmsd.Web.Pages.Tasks.DetailsModel>.Instance,
            configuration);

        // Act
        model.OnGet("TASK-001");

        // Assert
        model.LinksJson.Should().NotBeNullOrEmpty();
        model.LinksJson.Should().Contain("related");
    }

    [Fact]
    public void UatPage_RendersVerifications_FromUatSpec()
    {
        // Arrange
        CreateWorkspaceConfig();
        CreateUatSpec();
        CreateTasksSpec();
        
        var configuration = new ConfigurationBuilder().Build();
        var model = new Gmsd.Web.Pages.Uat.IndexModel(
            NullLogger<Gmsd.Web.Pages.Uat.IndexModel>.Instance,
            configuration);

        // Act
        model.OnGet();

        // Assert
        model.UatSessions.Should().NotBeNull();
    }

    [Fact]
    public void UatVerify_RendersChecks_FromTaskAcceptanceCriteria()
    {
        // Arrange
        CreateWorkspaceConfig();
        CreateTasksSpec();
        
        var configuration = new ConfigurationBuilder().Build();
        var model = new Gmsd.Web.Pages.Uat.VerifyModel(
            NullLogger<Gmsd.Web.Pages.Uat.VerifyModel>.Instance,
            configuration);

        // Act
        model.OnGet();

        // Assert
        model.Checks.Should().NotBeNull();
    }

    [Fact]
    public void IssuesPage_RendersIssues_FromIssuesSpec()
    {
        // Arrange
        CreateWorkspaceConfig();
        CreateIssuesSpec();
        
        var configuration = new ConfigurationBuilder().Build();
        var model = new Gmsd.Web.Pages.Issues.IndexModel(
            NullLogger<Gmsd.Web.Pages.Issues.IndexModel>.Instance,
            configuration);

        // Act
        model.OnGet();

        // Assert
        model.Issues.Should().HaveCount(2);
        model.Issues.First().Id.Should().Be("ISSUE-001");
        model.Issues.First().Title.Should().Be("Build fails on Windows");
    }

    [Fact]
    public void IssuesDetail_RendersReproSteps_FromIssuesSpec()
    {
        // Arrange
        CreateWorkspaceConfig();
        CreateIssuesSpec();
        
        var configuration = new ConfigurationBuilder().Build();
        var model = new Gmsd.Web.Pages.Issues.DetailsModel(
            NullLogger<Gmsd.Web.Pages.Issues.DetailsModel>.Instance,
            configuration);

        // Act
        model.OnGet("ISSUE-001");

        // Assert
        model.Issue.Should().NotBeNull();
        model.Issue!.ReproSteps.Should().Contain("Push to PR");
    }

    [Fact]
    public void IssuesDetail_RendersExpectedVsActual_FromIssuesSpec()
    {
        // Arrange
        CreateWorkspaceConfig();
        CreateIssuesSpec();
        
        var configuration = new ConfigurationBuilder().Build();
        var model = new Gmsd.Web.Pages.Issues.DetailsModel(
            NullLogger<Gmsd.Web.Pages.Issues.DetailsModel>.Instance,
            configuration);

        // Act
        model.OnGet("ISSUE-001");

        // Assert
        model.Issue.Should().NotBeNull();
        model.Issue!.ExpectedBehavior.Should().Be("Build should succeed on all platforms");
        model.Issue.ActualBehavior.Should().Be("Build fails with path separator error");
    }

    [Fact]
    public void IssuesDetail_LinksToTask_FromSpec()
    {
        // Arrange
        CreateWorkspaceConfig();
        CreateIssuesSpec();
        CreateTasksSpec();
        
        var configuration = new ConfigurationBuilder().Build();
        var model = new Gmsd.Web.Pages.Issues.DetailsModel(
            NullLogger<Gmsd.Web.Pages.Issues.DetailsModel>.Instance,
            configuration);

        // Act
        model.OnGet("ISSUE-001");

        // Assert
        model.Issue.Should().NotBeNull();
        model.Issue!.TaskId.Should().Be("TASK-001");
    }

    [Fact]
    public void AllPages_HandledMissingSpecFiles_Gracefully()
    {
        // Arrange - Don't create any spec files
        CreateWorkspaceConfig();
        
        var configuration = new ConfigurationBuilder().Build();
        
        // Act & Assert - All pages should handle missing specs gracefully
        var roadmapModel = new Gmsd.Web.Pages.Roadmap.IndexModel(
            NullLogger<Gmsd.Web.Pages.Roadmap.IndexModel>.Instance, configuration);
        roadmapModel.OnGet();
        roadmapModel.ErrorMessage.Should().NotBeNull();

        var tasksModel = new Gmsd.Web.Pages.Tasks.IndexModel(
            NullLogger<Gmsd.Web.Pages.Tasks.IndexModel>.Instance, configuration);
        tasksModel.OnGet();
        // Should not throw

        var issuesModel = new Gmsd.Web.Pages.Issues.IndexModel(
            NullLogger<Gmsd.Web.Pages.Issues.IndexModel>.Instance, configuration);
        issuesModel.OnGet();
        // Should not throw
    }

    [Fact]
    public void AllPages_RenderEmptyState_WhenNoData()
    {
        // Arrange - Create workspace but with empty specs
        CreateWorkspaceConfig();
        
        var roadmapFile = Path.Combine(_specDir, "roadmap.json");
        File.WriteAllText(roadmapFile, @"{""schemaVersion"": 1, ""roadmap"": {""title"": ""Empty"", ""items"": []}}");
        
        var configuration = new ConfigurationBuilder().Build();
        var model = new Gmsd.Web.Pages.Roadmap.IndexModel(
            NullLogger<Gmsd.Web.Pages.Roadmap.IndexModel>.Instance, configuration);

        // Act
        model.OnGet();

        // Assert
        model.Roadmap.Milestones.Should().BeEmpty();
    }
}
