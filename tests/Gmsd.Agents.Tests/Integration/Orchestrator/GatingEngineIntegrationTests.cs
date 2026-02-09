using FluentAssertions;
using Gmsd.Agents.Execution.ControlPlane;
using Gmsd.Agents.Tests.Fixtures;
using Gmsd.Aos.Contracts.State;
using Gmsd.Aos.Public;
using Xunit;

namespace Gmsd.Agents.Tests.Integration.Orchestrator;

/// <summary>
/// Integration tests for the GatingEngine that verify routing decisions based on workspace state.
/// Uses HandlerTestHost for DI container and AosTestWorkspaceBuilder for workspace simulation.
/// </summary>
public class GatingEngineIntegrationTests : IDisposable
{
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

        // Get the orchestrator from DI
        _sut = _testHost.GetRequiredService<IOrchestrator>();
    }

    public void Dispose()
    {
        // Dispose in reverse order of creation
        _testHost.Dispose();
        _workspaceBuilder.Dispose();
    }

    [Fact]
    public async Task EmptyWorkspace_RoutesToInterviewer()
    {
        // No files created - empty workspace
        var intent = new WorkflowIntent { InputRaw = "create project", CorrelationId = "corr-test" };

        var result = await _sut.ExecuteAsync(intent);

        result.IsSuccess.Should().BeTrue();
        result.FinalPhase.Should().Be("Interviewer");
    }

    [Fact]
    public async Task ProjectOnly_RoutesToRoadmapper()
    {
        // Create workspace with project
        var workspaceBuilder = new AosTestWorkspaceBuilder()
            .WithProject("Test Project", "Test project description");
        var workspace = workspaceBuilder.Build();

        using var testHost = new HandlerTestHost(workspace.RepositoryRootPath);
        testHost.OverrideWithInstance<IWorkspace>(workspace);

        var sut = testHost.GetRequiredService<IOrchestrator>();

        var intent = new WorkflowIntent { InputRaw = "create roadmap", CorrelationId = "corr-test" };

        var result = await sut.ExecuteAsync(intent);

        result.IsSuccess.Should().BeTrue();
        result.FinalPhase.Should().Be("Roadmapper");

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
            .WithRoadmap("Test Roadmap", []);
        var workspace = workspaceBuilder.Build();

        using var testHost = new HandlerTestHost(workspace.RepositoryRootPath);
        testHost.OverrideWithInstance<IWorkspace>(workspace);

        var sut = testHost.GetRequiredService<IOrchestrator>();

        var intent = new WorkflowIntent { InputRaw = "create plan", CorrelationId = "corr-test" };

        var result = await sut.ExecuteAsync(intent);

        result.IsSuccess.Should().BeTrue();
        result.FinalPhase.Should().Be("Planner");

        // Cleanup
        testHost.Dispose();
        workspaceBuilder.Dispose();
    }

    [Fact]
    public async Task CompletePlan_RoutesToExecutor()
    {
        // Create workspace with project, roadmap, and plan
        var workspaceBuilder = new AosTestWorkspaceBuilder()
            .WithProject("Test Project", "Test project description")
            .WithRoadmap("Test Roadmap", []);
        var workspace = workspaceBuilder.Build();

        // Create plan file
        var planPath = Path.Combine(workspaceBuilder.RepositoryRootPath, ".aos", "spec", "plan.json");
        Directory.CreateDirectory(Path.GetDirectoryName(planPath)!);
        File.WriteAllText(planPath, "{\"schemaVersion\": 1, \"plan\": {\"tasks\": []}}");

        using var testHost = new HandlerTestHost(workspace.RepositoryRootPath);
        testHost.OverrideWithInstance<IWorkspace>(workspace);

        var sut = testHost.GetRequiredService<IOrchestrator>();

        var intent = new WorkflowIntent { InputRaw = "execute", CorrelationId = "corr-test" };

        var result = await sut.ExecuteAsync(intent);

        result.IsSuccess.Should().BeTrue();
        result.FinalPhase.Should().Be("Executor");

        // Cleanup
        testHost.Dispose();
        workspaceBuilder.Dispose();
    }

    [Fact]
    public async Task ExecutionComplete_RoutesToVerifier()
    {
        // Create workspace with project, roadmap, plan, and execution state
        var workspaceBuilder = new AosTestWorkspaceBuilder()
            .WithProject("Test Project", "Test project description")
            .WithRoadmap("Test Roadmap", [])
            .WithState(new StateSnapshot
            {
                SchemaVersion = 1,
                Cursor = new StateCursor
                {
                    TaskId = "task-1",
                    TaskStatus = "completed",
                    StepStatus = null
                }
            });
        var workspace = workspaceBuilder.Build();

        // Create plan file
        var planPath = Path.Combine(workspaceBuilder.RepositoryRootPath, ".aos", "spec", "plan.json");
        Directory.CreateDirectory(Path.GetDirectoryName(planPath)!);
        File.WriteAllText(planPath, "{\"schemaVersion\": 1, \"plan\": {\"tasks\": []}}");

        using var testHost = new HandlerTestHost(workspace.RepositoryRootPath);
        testHost.OverrideWithInstance<IWorkspace>(workspace);

        var sut = testHost.GetRequiredService<IOrchestrator>();

        var intent = new WorkflowIntent { InputRaw = "verify", CorrelationId = "corr-test" };

        var result = await sut.ExecuteAsync(intent);

        result.IsSuccess.Should().BeTrue();
        result.FinalPhase.Should().Be("Verifier");

        // Cleanup
        testHost.Dispose();
        workspaceBuilder.Dispose();
    }

    [Fact]
    public async Task VerificationFailed_RoutesToFixPlanner()
    {
        // Create workspace with project, roadmap, plan, and failed verification state
        var workspaceBuilder = new AosTestWorkspaceBuilder()
            .WithProject("Test Project", "Test project description")
            .WithRoadmap("Test Roadmap", [])
            .WithState(new StateSnapshot
            {
                SchemaVersion = 1,
                Cursor = new StateCursor
                {
                    TaskId = "task-1",
                    TaskStatus = "completed",
                    StepStatus = "failed"
                }
            });
        var workspace = workspaceBuilder.Build();

        // Create plan file
        var planPath = Path.Combine(workspaceBuilder.RepositoryRootPath, ".aos", "spec", "plan.json");
        Directory.CreateDirectory(Path.GetDirectoryName(planPath)!);
        File.WriteAllText(planPath, "{\"schemaVersion\": 1, \"plan\": {\"tasks\": []}}");

        using var testHost = new HandlerTestHost(workspace.RepositoryRootPath);
        testHost.OverrideWithInstance<IWorkspace>(workspace);

        var sut = testHost.GetRequiredService<IOrchestrator>();

        var intent = new WorkflowIntent { InputRaw = "fix", CorrelationId = "corr-test" };

        var result = await sut.ExecuteAsync(intent);

        result.IsSuccess.Should().BeTrue();
        result.FinalPhase.Should().Be("FixPlanner");

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
                    TaskId = "task-1",
                    TaskStatus = "completed",
                    StepStatus = "failed"
                }
            });
        var workspace = workspaceBuilder.Build();

        using var testHost = new HandlerTestHost(workspace.RepositoryRootPath);
        testHost.OverrideWithInstance<IWorkspace>(workspace);

        var sut = testHost.GetRequiredService<IOrchestrator>();

        var intent = new WorkflowIntent { InputRaw = "something", CorrelationId = "corr-test" };

        var result = await sut.ExecuteAsync(intent);

        result.FinalPhase.Should().Be("Interviewer");

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
            .WithState(new StateSnapshot
            {
                SchemaVersion = 1,
                Cursor = new StateCursor
                {
                    TaskId = "task-1",
                    TaskStatus = "completed",
                    StepStatus = "failed"
                }
            });
        var workspace = workspaceBuilder.Build();

        using var testHost = new HandlerTestHost(workspace.RepositoryRootPath);
        testHost.OverrideWithInstance<IWorkspace>(workspace);

        var sut = testHost.GetRequiredService<IOrchestrator>();

        var intent = new WorkflowIntent { InputRaw = "something", CorrelationId = "corr-test" };

        var result = await sut.ExecuteAsync(intent);

        result.FinalPhase.Should().Be("Roadmapper");

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
            .WithRoadmap("Test Roadmap", [])
            .WithState(new StateSnapshot
            {
                SchemaVersion = 1,
                Cursor = new StateCursor
                {
                    TaskId = "task-1",
                    TaskStatus = "completed",
                    StepStatus = "failed"
                }
            });
        var workspace = workspaceBuilder.Build();

        using var testHost = new HandlerTestHost(workspace.RepositoryRootPath);
        testHost.OverrideWithInstance<IWorkspace>(workspace);

        var sut = testHost.GetRequiredService<IOrchestrator>();

        var intent = new WorkflowIntent { InputRaw = "something", CorrelationId = "corr-test" };

        var result = await sut.ExecuteAsync(intent);

        result.FinalPhase.Should().Be("Planner");

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
            .WithRoadmap("Test Roadmap", [])
            .WithState(new StateSnapshot
            {
                SchemaVersion = 1,
                Cursor = new StateCursor
                {
                    TaskId = "task-1",
                    TaskStatus = null,
                    StepStatus = "failed"
                }
            });
        var workspace = workspaceBuilder.Build();

        // Create plan file
        var planPath = Path.Combine(workspaceBuilder.RepositoryRootPath, ".aos", "spec", "plan.json");
        Directory.CreateDirectory(Path.GetDirectoryName(planPath)!);
        File.WriteAllText(planPath, "{\"schemaVersion\": 1, \"plan\": {\"tasks\": []}}");

        using var testHost = new HandlerTestHost(workspace.RepositoryRootPath);
        testHost.OverrideWithInstance<IWorkspace>(workspace);

        var sut = testHost.GetRequiredService<IOrchestrator>();

        var intent = new WorkflowIntent { InputRaw = "something", CorrelationId = "corr-test" };

        var result = await sut.ExecuteAsync(intent);

        result.FinalPhase.Should().Be("FixPlanner");

        // Cleanup
        testHost.Dispose();
        workspaceBuilder.Dispose();
    }
}
