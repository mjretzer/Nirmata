using System.Text.Json;
using FluentAssertions;
using ExecutionOrchestrator = nirmata.Agents.Execution.ControlPlane.Orchestrator;
using nirmata.Agents.Execution.Context;
using nirmata.Agents.Execution.ControlPlane;
using nirmata.Agents.Execution.ControlPlane.Chat;
using nirmata.Agents.Execution.Execution.TaskExecutor;
using nirmata.Agents.Execution.FixPlanner;
using nirmata.Agents.Execution.Planning;
using nirmata.Agents.Execution.Planning.PhasePlanner;
using nirmata.Agents.Execution.Preflight;
using nirmata.Agents.Execution.Validation;
using nirmata.Agents.Models.Runtime;
using nirmata.Agents.Persistence.Runs;
using nirmata.Aos.Contracts.Commands;
using nirmata.Aos.Contracts.State;
using nirmata.Aos.Public;
using nirmata.Aos.Public.Context.Packs;
using nirmata.Aos.Public.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace nirmata.Agents.Tests.Integration.Orchestrator;

public sealed class ExecutorRoutingTests : IDisposable
{
    private readonly string _workspaceRoot;
    private readonly string _aosRoot;
    private readonly Mock<IStateStore> _stateStore = new();
    private readonly Mock<IRunLifecycleManager> _runLifecycleManager = new();
    private readonly Mock<IGatingEngine> _gatingEngine = new();
    private readonly Mock<IContextPackManager> _contextPackManager = new();
    private readonly Mock<ITaskExecutor> _taskExecutor = new();
    private readonly Mock<IPrerequisiteValidator> _prerequisiteValidator = new();
    private readonly Mock<IPreflightValidator> _preflightValidator = new();
    private readonly Mock<IOutputValidator> _outputValidator = new();
    private readonly Mock<ICommandRouter> _commandRouter = new();
    private readonly Mock<IWorkspace> _workspace = new();

    public ExecutorRoutingTests()
    {
        _workspaceRoot = Path.Combine(Path.GetTempPath(), $"nirmata-executor-routing-{Guid.NewGuid():N}");
        _aosRoot = Path.Combine(_workspaceRoot, ".aos");

        Directory.CreateDirectory(Path.Combine(_aosRoot, "spec", "tasks"));
        Directory.CreateDirectory(Path.Combine(_aosRoot, "state"));

        _workspace.Setup(x => x.RepositoryRootPath).Returns(_workspaceRoot);
        _workspace.Setup(x => x.AosRootPath).Returns(_aosRoot);

        _runLifecycleManager
            .Setup(x => x.StartRunAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunContext { RunId = "RUN-EXECUTOR-001" });
        _runLifecycleManager
            .Setup(x => x.AttachInputAsync(It.IsAny<string>(), It.IsAny<WorkflowIntent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _runLifecycleManager
            .Setup(x => x.RecordCommandAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _runLifecycleManager
            .Setup(x => x.FinishRunAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _contextPackManager
            .Setup(x => x.CreatePackAsync("task", It.IsAny<string>(), It.IsAny<ContextPackBudget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ctx-pack-001");

        _prerequisiteValidator
            .Setup(x => x.EnsureWorkspaceInitializedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(PrerequisiteValidationResult.Satisfied("Executor"));
        _prerequisiteValidator
            .Setup(x => x.ValidateAsync(It.IsAny<string>(), It.IsAny<GatingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PrerequisiteValidationResult.Satisfied("Executor"));

        _preflightValidator
            .Setup(x => x.ValidateAsync(It.IsAny<WorkflowIntent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new nirmata.Agents.Execution.Validation.ValidationResult());

        _outputValidator
            .Setup(x => x.ValidateAsync(It.IsAny<OrchestratorResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new nirmata.Agents.Execution.Validation.ValidationResult());

        _gatingEngine
            .Setup(x => x.EvaluateAsync(It.IsAny<GatingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatingResult
            {
                TargetPhase = "Executor",
                Reason = "Ready to execute the plan",
                Reasoning = "Routing to Executor",
                RequiresConfirmation = false,
                ProposedAction = new nirmata.Agents.Execution.ControlPlane.ProposedAction
                {
                    Phase = "Executor",
                    Description = "Execute task plan",
                    RiskLevel = nirmata.Agents.Execution.ControlPlane.RiskLevel.WriteSafe,
                    SideEffects = Array.Empty<string>(),
                    AffectedResources = new[] { "workspace_files" }
                }
            });
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspaceRoot))
        {
            Directory.Delete(_workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenExecutorPhaseSelected_DispatchesRunExecuteForTaskPlan()
    {
        const string taskId = "TSK-EXEC-001";
        TaskExecutionRequest? capturedRequest = null;

        SeedState(new StateSnapshot
        {
            SchemaVersion = 1,
            Cursor = new StateCursor
            {
                MilestoneId = "MS-001",
                PhaseId = "PH-001",
                TaskId = taskId,
                MilestoneStatus = "active",
                PhaseStatus = "active",
                TaskStatus = null,
                StepStatus = null
            }
        });
        WriteTaskPlan(taskId);

        _taskExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<TaskExecutionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TaskExecutionRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new TaskExecutionResult
            {
                Success = true,
                RunId = "RUN-TASK-001",
                ModifiedFiles = new[] { "src/test.txt" }
            });

        var sut = CreateOrchestrator();

        var result = await sut.ExecuteAsync(new WorkflowIntent
        {
            InputRaw = "/run",
            CorrelationId = "corr-executor-001"
        });

        result.IsSuccess.Should().BeTrue();
        result.FinalPhase.Should().Be("Executor");
        result.Artifacts["commandGroup"].Should().Be("run");
        result.Artifacts["command"].Should().Be("execute");

        capturedRequest.Should().NotBeNull();
        capturedRequest!.TaskId.Should().Be(taskId);
        capturedRequest.AllowedFileScope.Should().ContainSingle().Which.Should().Be("src/test.txt");

        _runLifecycleManager.Verify(x => x.RecordCommandAsync(
            "RUN-EXECUTOR-001", "run", "execute", "dispatched", It.IsAny<CancellationToken>()), Times.Once);
        _runLifecycleManager.Verify(x => x.RecordCommandAsync(
            "RUN-EXECUTOR-001", "run", "execute", "completed", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFixTaskQueued_UsesFixTaskPlanAndClearsReadyMarker()
    {
        const string parentTaskId = "TSK-PARENT-001";
        const string fixTaskId = "TSK-FIX-TSK-PARENT-001-001-ABCD";
        TaskExecutionRequest? capturedRequest = null;

        SeedState(new StateSnapshot
        {
            SchemaVersion = 1,
            Cursor = new StateCursor
            {
                MilestoneId = "MS-001",
                PhaseId = "PH-001",
                TaskId = parentTaskId,
                TaskStatus = "fix-planned",
                StepId = fixTaskId,
                StepStatus = "ready-to-execute",
                MilestoneStatus = "active",
                PhaseStatus = "active"
            }
        });
        WriteTaskPlan(fixTaskId);

        _taskExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<TaskExecutionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TaskExecutionRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new TaskExecutionResult
            {
                Success = true,
                RunId = "RUN-FIX-001",
                ModifiedFiles = new[] { "src/test.txt" }
            });

        var sut = CreateOrchestrator();

        var result = await sut.ExecuteAsync(new WorkflowIntent
        {
            InputRaw = "/run",
            CorrelationId = "corr-executor-fix-001"
        });

        result.IsSuccess.Should().BeTrue();
        result.FinalPhase.Should().Be("Executor");

        capturedRequest.Should().NotBeNull();
        capturedRequest!.TaskId.Should().Be(fixTaskId);
        capturedRequest.TaskDirectory.Should().EndWith(Path.Combine(".aos", "spec", "tasks", fixTaskId));

        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(_aosRoot, "state", "state.json")));
        var cursor = document.RootElement.GetProperty("cursor");
        cursor.GetProperty("taskId").GetString().Should().Be(parentTaskId);
        cursor.GetProperty("taskStatus").GetString().Should().Be("completed");
        cursor.GetProperty("stepId").GetString().Should().Be(fixTaskId);
        cursor.GetProperty("stepStatus").ValueKind.Should().Be(JsonValueKind.Null);
    }

    private ExecutionOrchestrator CreateOrchestrator()
    {
        var taskExecutorHandler = new TaskExecutorHandler(_taskExecutor.Object, _workspace.Object, _stateStore.Object);
        var interviewerHandler = new InterviewerHandler(Mock.Of<INewProjectInterviewer>(), Mock.Of<IInterviewEvidenceWriter>());
        var responderHandler = new ResponderHandler(Mock.Of<IChatResponder>());
        var roadmapperHandler = new RoadmapperHandler(Mock.Of<IRoadmapper>(), Mock.Of<nirmata.Agents.Execution.Planning.IRoadmapGenerator>(), _workspace.Object);
        var phasePlannerHandler = new PhasePlannerHandler(
            Mock.Of<nirmata.Agents.Execution.Planning.PhasePlanner.ContextGatherer.IPhaseContextGatherer>(),
            Mock.Of<IPhasePlanner>(),
            Mock.Of<nirmata.Agents.Execution.Planning.PhasePlanner.Assumptions.IPhaseAssumptionLister>(),
            _runLifecycleManager.Object,
            Mock.Of<IEventStore>());
        var verifierHandler = new VerifierHandler(
            Mock.Of<nirmata.Agents.Execution.Verification.UatVerifier.IUatVerifier>(),
            _workspace.Object,
            _stateStore.Object,
            _runLifecycleManager.Object);
        var fixPlannerHandler = new FixPlannerHandler(
            Mock.Of<IFixPlanner>(),
            _workspace.Object,
            _stateStore.Object,
            _runLifecycleManager.Object,
            Mock.Of<ILogger<FixPlannerHandler>>());
        var atomicGitCommitterHandler = new AtomicGitCommitterHandler(
            Mock.Of<nirmata.Agents.Execution.Execution.AtomicGitCommitter.IAtomicGitCommitter>(),
            _workspace.Object,
            _stateStore.Object);

        return new ExecutionOrchestrator(
            _gatingEngine.Object,
            _commandRouter.Object,
            _workspace.Object,
            Mock.Of<ISpecStore>(),
            _stateStore.Object,
            Mock.Of<IValidator>(),
            _runLifecycleManager.Object,
            Mock.Of<IConfirmationGate>(),
            interviewerHandler,
            roadmapperHandler,
            phasePlannerHandler,
            taskExecutorHandler,
            verifierHandler,
            fixPlannerHandler,
            atomicGitCommitterHandler,
            _preflightValidator.Object,
            _prerequisiteValidator.Object,
            _outputValidator.Object,
            _contextPackManager.Object,
            responderHandler,
            new InputClassifier(),
            new ChatResponder(Mock.Of<IChatResponder>()),
            new ReadOnlyHandler(),
            Mock.Of<nirmata.Agents.Execution.Continuity.HistoryWriter.IHistoryWriter>(),
            Mock.Of<nirmata.Agents.Execution.Continuity.IHandoffStateStore>());
    }

    private void SeedState(StateSnapshot snapshot)
    {
        _stateStore.Setup(x => x.ReadSnapshot()).Returns(snapshot);
        var statePath = Path.Combine(_aosRoot, "state", "state.json");
        File.WriteAllText(statePath, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        }));
    }

    private void WriteTaskPlan(string taskId)
    {
        var taskDir = Path.Combine(_aosRoot, "spec", "tasks", taskId);
        Directory.CreateDirectory(taskDir);
        File.WriteAllText(Path.Combine(taskDir, "plan.json"),
            """
            {
              "schemaVersion": 1,
              "taskId": "TASK-ID",
              "title": "Task TASK-ID",
              "description": "Task plan",
              "fileScopes": [
                {
                  "path": "src/test.txt",
                  "scopeType": "create"
                }
              ],
              "verificationSteps": [
                {
                  "verificationType": "file",
                  "description": "Verify file creation"
                }
              ]
            }
            """.Replace("TASK-ID", taskId, StringComparison.Ordinal));
    }
}