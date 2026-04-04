using FluentAssertions;
using nirmata.Agents.Execution.ControlPlane;
using nirmata.Agents.Execution.Validation;
using nirmata.Agents.Tests.Fixtures;
using nirmata.Aos.Contracts.State;
using nirmata.Aos.Public;
using Xunit;

namespace nirmata.Agents.Tests.Integration.Orchestrator;

/// <summary>
/// Integration tests for the GatingEngine that verify routing decisions based on workspace state.
/// Uses HandlerTestHost for DI container and AosTestWorkspaceBuilder for workspace simulation.
/// </summary>
public class GatingEngineIntegrationTests : IDisposable
{
    private static readonly object[] RoadmapItems = [new { id = "phase-1", title = "Phase 1" }];
    private const string MilestoneId = "MS-0001";
    private const string PhaseId = "PH-0001";
    private const string TaskId = "TSK-000001";

    private readonly AosTestWorkspaceBuilder _workspaceBuilder;
    private readonly HandlerTestHost _testHost;
    private readonly IOrchestrator _sut;

    public GatingEngineIntegrationTests()
    {
        // Create workspace builder (empty by default for gating tests)
        _workspaceBuilder = new AosTestWorkspaceBuilder();

        // Build the workspace to create the temp directory structure
        var workspace = _workspaceBuilder.Build();

        // Create test host with the workspace path
        _testHost = new HandlerTestHost(workspace.RepositoryRootPath);

        // Override the IWorkspace registration with our test workspace
        _testHost.OverrideWithInstance<IWorkspace>(workspace);
        _testHost.OverrideWithInstance<IPrerequisiteValidator>(new AlwaysReadyPrerequisiteValidator());

        // Get the orchestrator from DI
        _sut = _testHost.GetRequiredService<IOrchestrator>();
    }

    public void Dispose()
    {
        // Dispose in reverse order of creation
        _testHost.Dispose();
        _workspaceBuilder.Dispose();
    }

    private static void AssertFinalPhase(OrchestratorResult result, string expectedPhase)
    {
        var error = result.Artifacts.TryGetValue("error", out var errorValue)
            ? errorValue?.ToString()
            : null;

        result.FinalPhase.Should().Be(expectedPhase, error is null ? string.Empty : $"error: {error}");
    }

    [Fact]
    public async Task EmptyWorkspace_RoutesToInterviewer()
    {
        // No files created - empty workspace
        var intent = new WorkflowIntent { InputRaw = "/run", CorrelationId = "corr-test" };

        var result = await _sut.ExecuteAsync(intent);

        result.IsSuccess.Should().BeTrue();
        AssertFinalPhase(result, "Interviewer");
    }

    [Fact]
    public async Task ProjectOnly_RoutesToRoadmapper()
    {
        // Create workspace with project
        var workspaceBuilder = new AosTestWorkspaceBuilder()
            .WithProject("Test Project", "Test project description")
            .WithCodebaseIntelligence();
        var workspace = workspaceBuilder.Build();

        using var testHost = new HandlerTestHost(workspace.RepositoryRootPath);
        testHost.OverrideWithInstance<IWorkspace>(workspace);
        testHost.OverrideWithInstance<IPrerequisiteValidator>(new AlwaysReadyPrerequisiteValidator());

        var sut = testHost.GetRequiredService<IOrchestrator>();

        var intent = new WorkflowIntent { InputRaw = "/run", CorrelationId = "corr-test" };

        var result = await sut.ExecuteAsync(intent);

        AssertFinalPhase(result, "Roadmapper");

        // Cleanup
        testHost.Dispose();
        workspaceBuilder.Dispose();
    }

    [Fact]
    public async Task ProjectAndRoadmap_RoutesToPlanner()
    {
        // Create workspace with project and roadmap
        var workspaceBuilder = new AosTestWorkspaceBuilder()
            .WithProject("Test Project", "Test project description")
            .WithRoadmap("Test Roadmap", RoadmapItems)
            .WithCodebaseIntelligence();
        var workspace = workspaceBuilder.Build();

        using var testHost = new HandlerTestHost(workspace.RepositoryRootPath);
        testHost.OverrideWithInstance<IWorkspace>(workspace);
        testHost.OverrideWithInstance<IPrerequisiteValidator>(new AlwaysReadyPrerequisiteValidator());

        var sut = testHost.GetRequiredService<IOrchestrator>();

        var intent = new WorkflowIntent { InputRaw = "/run", CorrelationId = "corr-test" };

        var result = await sut.ExecuteAsync(intent);

        AssertFinalPhase(result, "Planner");

        // Cleanup
        testHost.Dispose();
        workspaceBuilder.Dispose();
    }

    [Fact]
    public async Task CompletePlan_RoutesToExecutor()
    {
        // Create workspace with project, roadmap, and a canonical task-scoped plan
        var workspaceBuilder = new AosTestWorkspaceBuilder()
            .WithProject("Test Project", "Test project description")
            .WithRoadmap("Test Roadmap", RoadmapItems)
            .WithState(new StateSnapshot
            {
                SchemaVersion = 1,
                Cursor = new StateCursor
                {
                    MilestoneId = MilestoneId,
                    PhaseId = PhaseId,
                    TaskId = TaskId
                }
            })
            .WithTaskPlan(TaskId);
        var workspace = workspaceBuilder.Build();

        using var testHost = new HandlerTestHost(workspace.RepositoryRootPath);
        testHost.OverrideWithInstance<IWorkspace>(workspace);
        testHost.OverrideWithInstance<IPrerequisiteValidator>(new AlwaysReadyPrerequisiteValidator());

        var sut = testHost.GetRequiredService<IOrchestrator>();

        var intent = new WorkflowIntent { InputRaw = "/run", CorrelationId = "corr-test" };

        var result = await sut.ExecuteAsync(intent);

        AssertFinalPhase(result, "Executor");

        // Cleanup
        testHost.Dispose();
        workspaceBuilder.Dispose();
    }

    [Fact]
    public async Task ExecutionComplete_RoutesToVerifier()
    {
        // Create workspace with project, roadmap, task plan, and execution state
        var workspaceBuilder = new AosTestWorkspaceBuilder()
            .WithProject("Test Project", "Test project description")
            .WithRoadmap("Test Roadmap", RoadmapItems)
            .WithState(new StateSnapshot
            {
                SchemaVersion = 1,
                Cursor = new StateCursor
                {
                    MilestoneId = MilestoneId,
                    PhaseId = PhaseId,
                    TaskId = TaskId,
                    TaskStatus = "completed",
                    StepStatus = null
                }
            })
            .WithTaskPlan(TaskId);
        var workspace = workspaceBuilder.Build();

        using var testHost = new HandlerTestHost(workspace.RepositoryRootPath);
        testHost.OverrideWithInstance<IWorkspace>(workspace);
        testHost.OverrideWithInstance<IPrerequisiteValidator>(new AlwaysReadyPrerequisiteValidator());

        var sut = testHost.GetRequiredService<IOrchestrator>();

        var intent = new WorkflowIntent { InputRaw = "/run", CorrelationId = "corr-test" };

        var result = await sut.ExecuteAsync(intent);

        AssertFinalPhase(result, "Verifier");

        // Cleanup
        testHost.Dispose();
        workspaceBuilder.Dispose();
    }

    [Fact]
    public async Task VerificationFailed_RoutesToFixPlanner()
    {
        // Create workspace with project, roadmap, task plan, and failed verification state
        var workspaceBuilder = new AosTestWorkspaceBuilder()
            .WithProject("Test Project", "Test project description")
            .WithRoadmap("Test Roadmap", RoadmapItems)
            .WithState(new StateSnapshot
            {
                SchemaVersion = 1,
                Cursor = new StateCursor
                {
                    MilestoneId = MilestoneId,
                    PhaseId = PhaseId,
                    TaskId = TaskId,
                    TaskStatus = "completed",
                    StepStatus = "failed"
                }
            })
            .WithTaskPlan(TaskId);
        var workspace = workspaceBuilder.Build();

        using var testHost = new HandlerTestHost(workspace.RepositoryRootPath);
        testHost.OverrideWithInstance<IWorkspace>(workspace);
        testHost.OverrideWithInstance<IPrerequisiteValidator>(new AlwaysReadyPrerequisiteValidator());

        var sut = testHost.GetRequiredService<IOrchestrator>();

        var intent = new WorkflowIntent { InputRaw = "/run", CorrelationId = "corr-test" };

        var result = await sut.ExecuteAsync(intent);

        AssertFinalPhase(result, "FixPlanner");

        // Cleanup
        testHost.Dispose();
        workspaceBuilder.Dispose();
    }

    [Fact]
    public async Task PriorityOrderEnforced_MissingProjectTakesPrecedenceOverOtherStates()
    {
        // No project file, but state indicates completed execution with failed verification
        // Project should take precedence (Interviewer, not FixPlanner)
        var workspaceBuilder = new AosTestWorkspaceBuilder()
            .WithState(new StateSnapshot
            {
                SchemaVersion = 1,
                Cursor = new StateCursor
                {
                    MilestoneId = MilestoneId,
                    PhaseId = PhaseId,
                    TaskId = TaskId,
                    TaskStatus = "completed",
                    StepStatus = "failed"
                }
            });
        var workspace = workspaceBuilder.Build();

        using var testHost = new HandlerTestHost(workspace.RepositoryRootPath);
        testHost.OverrideWithInstance<IWorkspace>(workspace);
        testHost.OverrideWithInstance<IPrerequisiteValidator>(new AlwaysReadyPrerequisiteValidator());

        var sut = testHost.GetRequiredService<IOrchestrator>();

        var intent = new WorkflowIntent { InputRaw = "/run", CorrelationId = "corr-test" };

        var result = await sut.ExecuteAsync(intent);

        AssertFinalPhase(result, "Interviewer");

        // Cleanup
        testHost.Dispose();
        workspaceBuilder.Dispose();
    }

    [Fact]
    public async Task PriorityOrderEnforced_MissingRoadmapTakesPrecedenceOverPlanStates()
    {
        // Project exists but no roadmap, state shows completed execution
        var workspaceBuilder = new AosTestWorkspaceBuilder()
            .WithProject("Test Project", "Test project description")
            .WithCodebaseIntelligence()
            .WithState(new StateSnapshot
            {
                SchemaVersion = 1,
                Cursor = new StateCursor
                {
                    MilestoneId = MilestoneId,
                    PhaseId = PhaseId,
                    TaskId = TaskId,
                    TaskStatus = "completed",
                    StepStatus = "failed"
                }
            });
        var workspace = workspaceBuilder.Build();

        using var testHost = new HandlerTestHost(workspace.RepositoryRootPath);
        testHost.OverrideWithInstance<IWorkspace>(workspace);
        testHost.OverrideWithInstance<IPrerequisiteValidator>(new AlwaysReadyPrerequisiteValidator());

        var sut = testHost.GetRequiredService<IOrchestrator>();

        var intent = new WorkflowIntent { InputRaw = "/run", CorrelationId = "corr-test" };

        var result = await sut.ExecuteAsync(intent);

        AssertFinalPhase(result, "Roadmapper");

        // Cleanup
        testHost.Dispose();
        workspaceBuilder.Dispose();
    }

    [Fact]
    public async Task PriorityOrderEnforced_MissingPlanTakesPrecedenceOverExecutionStates()
    {
        // Project and roadmap exist but no plan, state shows completed execution with failure
        var workspaceBuilder = new AosTestWorkspaceBuilder()
            .WithProject("Test Project", "Test project description")
            .WithRoadmap("Test Roadmap", RoadmapItems)
            .WithCodebaseIntelligence()
            .WithState(new StateSnapshot
            {
                SchemaVersion = 1,
                Cursor = new StateCursor
                {
                    MilestoneId = MilestoneId,
                    PhaseId = PhaseId,
                    TaskId = TaskId,
                    TaskStatus = "completed",
                    StepStatus = "failed"
                }
            });
        var workspace = workspaceBuilder.Build();

        using var testHost = new HandlerTestHost(workspace.RepositoryRootPath);
        testHost.OverrideWithInstance<IWorkspace>(workspace);
        testHost.OverrideWithInstance<IPrerequisiteValidator>(new AlwaysReadyPrerequisiteValidator());

        var sut = testHost.GetRequiredService<IOrchestrator>();

        var intent = new WorkflowIntent { InputRaw = "/run", CorrelationId = "corr-test" };

        var result = await sut.ExecuteAsync(intent);

        AssertFinalPhase(result, "Planner");

        // Cleanup
        testHost.Dispose();
        workspaceBuilder.Dispose();
    }

    [Fact]
    public async Task PriorityOrderEnforced_VerificationFailedTakesPrecedenceOverExecutor()
    {
        // All artifacts exist, verification failed - should route to FixPlanner not Executor
        var workspaceBuilder = new AosTestWorkspaceBuilder()
            .WithProject("Test Project", "Test project description")
            .WithRoadmap("Test Roadmap", RoadmapItems)
            .WithState(new StateSnapshot
            {
                SchemaVersion = 1,
                Cursor = new StateCursor
                {
                    MilestoneId = MilestoneId,
                    PhaseId = PhaseId,
                    TaskId = TaskId,
                    TaskStatus = null,
                    StepStatus = "failed"
                }
            })
            .WithTaskPlan(TaskId);
        var workspace = workspaceBuilder.Build();

        using var testHost = new HandlerTestHost(workspace.RepositoryRootPath);
        testHost.OverrideWithInstance<IWorkspace>(workspace);
        testHost.OverrideWithInstance<IPrerequisiteValidator>(new AlwaysReadyPrerequisiteValidator());

        var sut = testHost.GetRequiredService<IOrchestrator>();

        var intent = new WorkflowIntent { InputRaw = "/run", CorrelationId = "corr-test" };

        var result = await sut.ExecuteAsync(intent);

        AssertFinalPhase(result, "FixPlanner");

        // Cleanup
        testHost.Dispose();
        workspaceBuilder.Dispose();
    }

    [Fact]
    public async Task VerificationPassed_WithRemainingPhaseWork_RoutesToExecutor()
    {
        var workspaceBuilder = new AosTestWorkspaceBuilder()
            .WithProject("Test Project", "Test project description")
            .WithRoadmap("Test Roadmap", RoadmapItems)
            .WithState(new StateSnapshot
            {
                SchemaVersion = 1,
                Cursor = new StateCursor
                {
                    MilestoneId = MilestoneId,
                    PhaseId = PhaseId,
                    TaskId = TaskId,
                    TaskStatus = "completed",
                    StepStatus = "passed",
                    PhaseStatus = "active",
                    MilestoneStatus = "active"
                }
            })
            .WithTaskPlan(TaskId);
        var workspace = workspaceBuilder.Build();

        using var testHost = new HandlerTestHost(workspace.RepositoryRootPath);
        testHost.OverrideWithInstance<IWorkspace>(workspace);
        testHost.OverrideWithInstance<IPrerequisiteValidator>(new AlwaysReadyPrerequisiteValidator());

        var sut = testHost.GetRequiredService<IOrchestrator>();

        var intent = new WorkflowIntent { InputRaw = "/run", CorrelationId = "corr-test" };

        var result = await sut.ExecuteAsync(intent);

        AssertFinalPhase(result, "Executor");

        testHost.Dispose();
        workspaceBuilder.Dispose();
    }

    [Fact]
    public async Task VerificationPassed_WhenPhaseComplete_RoutesToPlanner()
    {
        var workspaceBuilder = new AosTestWorkspaceBuilder()
            .WithProject("Test Project", "Test project description")
            .WithRoadmap("Test Roadmap", RoadmapItems)
            .WithState(new StateSnapshot
            {
                SchemaVersion = 1,
                Cursor = new StateCursor
                {
                    MilestoneId = MilestoneId,
                    PhaseId = PhaseId,
                    TaskId = TaskId,
                    TaskStatus = "completed",
                    StepStatus = "passed",
                    PhaseStatus = "verified-pass",
                    MilestoneStatus = "active"
                }
            })
            .WithTaskPlan(TaskId);
        var workspace = workspaceBuilder.Build();

        using var testHost = new HandlerTestHost(workspace.RepositoryRootPath);
        testHost.OverrideWithInstance<IWorkspace>(workspace);
        testHost.OverrideWithInstance<IPrerequisiteValidator>(new AlwaysReadyPrerequisiteValidator());

        var sut = testHost.GetRequiredService<IOrchestrator>();

        var intent = new WorkflowIntent { InputRaw = "/run", CorrelationId = "corr-test" };

        var result = await sut.ExecuteAsync(intent);

        AssertFinalPhase(result, "Planner");

        testHost.Dispose();
        workspaceBuilder.Dispose();
    }

    [Fact]
    public async Task VerificationPassed_WhenMilestoneComplete_RoutesToMilestoneProgression()
    {
        var workspaceBuilder = new AosTestWorkspaceBuilder()
            .WithProject("Test Project", "Test project description")
            .WithRoadmap("Test Roadmap", RoadmapItems)
            .WithState(new StateSnapshot
            {
                SchemaVersion = 1,
                Cursor = new StateCursor
                {
                    MilestoneId = MilestoneId,
                    PhaseId = PhaseId,
                    TaskId = TaskId,
                    TaskStatus = "completed",
                    StepStatus = "passed",
                    PhaseStatus = "verified-pass",
                    MilestoneStatus = "completed"
                }
            })
            .WithTaskPlan(TaskId);
        var workspace = workspaceBuilder.Build();

        using var testHost = new HandlerTestHost(workspace.RepositoryRootPath);
        testHost.OverrideWithInstance<IWorkspace>(workspace);
        testHost.OverrideWithInstance<IPrerequisiteValidator>(new AlwaysReadyPrerequisiteValidator());

        var sut = testHost.GetRequiredService<IOrchestrator>();

        var intent = new WorkflowIntent { InputRaw = "/run", CorrelationId = "corr-test" };

        var result = await sut.ExecuteAsync(intent);

        AssertFinalPhase(result, "MilestoneProgression");

        testHost.Dispose();
        workspaceBuilder.Dispose();
    }
}
