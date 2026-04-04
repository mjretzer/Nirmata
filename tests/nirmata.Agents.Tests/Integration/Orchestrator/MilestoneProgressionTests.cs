using System.Text.Json;
using FluentAssertions;
using ExecutionOrchestrator = nirmata.Agents.Execution.ControlPlane.Orchestrator;
using nirmata.Agents.Execution.ControlPlane;
using nirmata.Agents.Execution.ControlPlane.Chat;
using nirmata.Agents.Execution.Continuity;
using nirmata.Agents.Execution.Continuity.HistoryWriter;
using nirmata.Agents.Execution.FixPlanner;
using nirmata.Agents.Execution.Planning;
using nirmata.Agents.Execution.Planning.PhasePlanner;
using nirmata.Agents.Execution.Validation;
using nirmata.Agents.Execution.Context;
using nirmata.Agents.Execution.Preflight;
using nirmata.Agents.Models.Runtime;
using nirmata.Agents.Persistence.Runs;
using nirmata.Aos.Contracts.Commands;
using nirmata.Aos.Contracts.State;
using nirmata.Aos.Public;
using nirmata.Aos.Public.Services;
using Moq;
using Xunit;
using ControlPlaneProposedAction = nirmata.Agents.Execution.ControlPlane.ProposedAction;

namespace nirmata.Agents.Tests.Integration.Orchestrator;

/// <summary>
/// Tests for milestone progression handling in the Orchestrator.
/// Verifies that when the gating engine routes to "MilestoneProgression",
/// the orchestrator handles it via its dedicated lightweight transition method
/// (not the generic command router).
/// </summary>
public class MilestoneProgressionTests
{
    private readonly Mock<IStateStore> _stateStore;
    private readonly Mock<IRunLifecycleManager> _runLifecycleManager;
    private readonly Mock<IGatingEngine> _gatingEngine;
    private readonly Mock<ICommandRouter> _commandRouter;
    private readonly List<JsonElement> _appendedEvents = new();

    private readonly ExecutionOrchestrator _sut;

    public MilestoneProgressionTests()
    {
        _stateStore = new Mock<IStateStore>();
        _runLifecycleManager = new Mock<IRunLifecycleManager>();
        _gatingEngine = new Mock<IGatingEngine>();
        _commandRouter = new Mock<ICommandRouter>();

        var workspace = new Mock<IWorkspace>();
        var specStore = new Mock<ISpecStore>();
        var validator = new Mock<IValidator>();
        var confirmationGate = new Mock<IConfirmationGate>();
        var preflightValidator = new Mock<IPreflightValidator>();
        var prerequisiteValidator = new Mock<IPrerequisiteValidator>();
        var outputValidator = new Mock<IOutputValidator>();
        var contextPackManager = new Mock<IContextPackManager>();

        // Default state: milestone MS-0001 is complete with verification passed
        _stateStore.Setup(s => s.ReadSnapshot()).Returns(new StateSnapshot
        {
            SchemaVersion = 1,
            Cursor = new StateCursor
            {
                MilestoneId = "MS-0001",
                PhaseId = "PH-0001",
                TaskId = "TSK-000001",
                MilestoneStatus = "completed",
                PhaseStatus = "verified-pass",
                TaskStatus = "completed",
                StepStatus = "passed"
            }
        });

        // Capture appended events for verification
        _stateStore
            .Setup(s => s.AppendEvent(It.IsAny<JsonElement>()))
            .Callback<JsonElement>(e => _appendedEvents.Add(e.Clone()));

        // Workspace
        workspace.Setup(w => w.AosRootPath).Returns("/fake/.aos");

        // Prerequisite validator: satisfied
        prerequisiteValidator
            .Setup(v => v.EnsureWorkspaceInitializedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(PrerequisiteValidationResult.Satisfied("MilestoneProgression"));
        prerequisiteValidator
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<GatingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PrerequisiteValidationResult.Satisfied("MilestoneProgression"));

        // Preflight validator: valid
        preflightValidator
            .Setup(v => v.ValidateAsync(It.IsAny<WorkflowIntent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new nirmata.Agents.Execution.Validation.ValidationResult());

        // Output validator: valid
        outputValidator
            .Setup(v => v.ValidateAsync(It.IsAny<OrchestratorResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new nirmata.Agents.Execution.Validation.ValidationResult());

        // Gating engine: route to MilestoneProgression
        _gatingEngine
            .Setup(g => g.EvaluateAsync(It.IsAny<GatingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatingResult
            {
                TargetPhase = "MilestoneProgression",
                Reason = "All phases in current milestone verified",
                Reasoning = "Routing to MilestoneProgression",
                RequiresConfirmation = false,
                ProposedAction = new ControlPlaneProposedAction
                {
                    Phase = "MilestoneProgression",
                    Description = "Record milestone completion",
                    RiskLevel = nirmata.Agents.Execution.ControlPlane.RiskLevel.WriteSafe,
                    SideEffects = Array.Empty<string>(),
                    AffectedResources = new[] { ".aos/state/state.json" }
                },
                ContextData = new Dictionary<string, object> { ["milestoneComplete"] = true }
            });

        // Run lifecycle
        _runLifecycleManager
            .Setup(r => r.StartRunAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunContext { RunId = "RUN-test-001" });
        _runLifecycleManager
            .Setup(r => r.AttachInputAsync(It.IsAny<string>(), It.IsAny<WorkflowIntent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _runLifecycleManager
            .Setup(r => r.RecordCommandAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _runLifecycleManager
            .Setup(r => r.FinishRunAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Create real handler instances with mocked dependencies (handlers are sealed, can't be mocked)
        var interviewerHandler = new InterviewerHandler(
            Mock.Of<INewProjectInterviewer>(),
            Mock.Of<IInterviewEvidenceWriter>());
        var responderHandler = new ResponderHandler(Mock.Of<IChatResponder>());
        var roadmapperHandler = new RoadmapperHandler(
            Mock.Of<IRoadmapper>(),
            Mock.Of<nirmata.Agents.Execution.Planning.IRoadmapGenerator>(),
            workspace.Object);
        var phasePlannerHandler = new PhasePlannerHandler(
            Mock.Of<nirmata.Agents.Execution.Planning.PhasePlanner.ContextGatherer.IPhaseContextGatherer>(),
            Mock.Of<IPhasePlanner>(),
            Mock.Of<nirmata.Agents.Execution.Planning.PhasePlanner.Assumptions.IPhaseAssumptionLister>(),
            _runLifecycleManager.Object,
            Mock.Of<IEventStore>());
        var taskExecutorHandler = new TaskExecutorHandler(
            Mock.Of<nirmata.Agents.Execution.Execution.TaskExecutor.ITaskExecutor>(),
            workspace.Object,
            _stateStore.Object);
        var verifierHandler = new VerifierHandler(
            Mock.Of<nirmata.Agents.Execution.Verification.UatVerifier.IUatVerifier>(),
            workspace.Object,
            _stateStore.Object,
            _runLifecycleManager.Object);
        var fixPlannerHandler = new FixPlannerHandler(
            Mock.Of<IFixPlanner>(),
            workspace.Object,
            _stateStore.Object,
            _runLifecycleManager.Object,
            Mock.Of<Microsoft.Extensions.Logging.ILogger<FixPlannerHandler>>());
        var atomicGitCommitterHandler = new AtomicGitCommitterHandler(
            Mock.Of<nirmata.Agents.Execution.Execution.AtomicGitCommitter.IAtomicGitCommitter>(),
            workspace.Object,
            _stateStore.Object);
        var inputClassifier = new InputClassifier();
        var chatResponder = new ChatResponder(Mock.Of<IChatResponder>());
        var readOnlyHandler = new ReadOnlyHandler();

        _sut = new ExecutionOrchestrator(
            _gatingEngine.Object,
            _commandRouter.Object,
            workspace.Object,
            specStore.Object,
            _stateStore.Object,
            validator.Object,
            _runLifecycleManager.Object,
            confirmationGate.Object,
            interviewerHandler,
            roadmapperHandler,
            phasePlannerHandler,
            taskExecutorHandler,
            verifierHandler,
            fixPlannerHandler,
            atomicGitCommitterHandler,
            preflightValidator.Object,
            prerequisiteValidator.Object,
            outputValidator.Object,
            contextPackManager.Object,
            responderHandler,
            inputClassifier,
            chatResponder,
            readOnlyHandler,
            Mock.Of<IHistoryWriter>(),
            Mock.Of<IHandoffStateStore>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenMilestoneComplete_RoutesToMilestoneProgression()
    {
        var intent = new WorkflowIntent
        {
            InputRaw = "/run",
            CorrelationId = "corr-milestone-001"
        };

        var result = await _sut.ExecuteAsync(intent);

        result.FinalPhase.Should().Be("MilestoneProgression");
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenMilestoneComplete_ReturnsMilestoneIdInArtifacts()
    {
        var intent = new WorkflowIntent
        {
            InputRaw = "/run",
            CorrelationId = "corr-milestone-002"
        };

        var result = await _sut.ExecuteAsync(intent);

        result.Artifacts.Should().ContainKey("milestoneId");
        result.Artifacts["milestoneId"].Should().Be("MS-0001");
    }

    [Fact]
    public async Task ExecuteAsync_WhenMilestoneComplete_ReturnsDoneStatus()
    {
        var intent = new WorkflowIntent
        {
            InputRaw = "/run",
            CorrelationId = "corr-milestone-003"
        };

        var result = await _sut.ExecuteAsync(intent);

        result.Artifacts.Should().ContainKey("milestoneStatus");
        result.Artifacts["milestoneStatus"].Should().Be("done");
    }

    [Fact]
    public async Task ExecuteAsync_WhenMilestoneComplete_AppendsCursorSetEvent()
    {
        var intent = new WorkflowIntent
        {
            InputRaw = "/run",
            CorrelationId = "corr-milestone-004"
        };

        await _sut.ExecuteAsync(intent);

        // Find the cursor.set event in appended events
        var cursorSetEvent = _appendedEvents.FirstOrDefault(e =>
            e.TryGetProperty("eventType", out var et) && et.GetString() == "cursor.set");
        cursorSetEvent.ValueKind.Should().NotBe(JsonValueKind.Undefined, "cursor.set event should be appended");

        var cursor = cursorSetEvent.GetProperty("cursor");
        cursor.GetProperty("milestoneStatus").GetString().Should().Be("done");
        cursor.GetProperty("phaseStatus").GetString().Should().Be("done");
    }

    [Fact]
    public async Task ExecuteAsync_WhenMilestoneComplete_AppendsMilestoneCompletedEvent()
    {
        var intent = new WorkflowIntent
        {
            InputRaw = "/run",
            CorrelationId = "corr-milestone-005"
        };

        await _sut.ExecuteAsync(intent);

        // Find the milestone.completed event in appended events
        var milestoneEvent = _appendedEvents.FirstOrDefault(e =>
            e.TryGetProperty("eventType", out var et) && et.GetString() == "milestone.completed");
        milestoneEvent.ValueKind.Should().NotBe(JsonValueKind.Undefined, "milestone.completed event should be appended");
        milestoneEvent.GetProperty("milestoneId").GetString().Should().Be("MS-0001");
    }

    [Fact]
    public async Task ExecuteAsync_WhenMilestoneComplete_RecordsCommandInRunLifecycle()
    {
        var intent = new WorkflowIntent
        {
            InputRaw = "/run",
            CorrelationId = "corr-milestone-006"
        };

        await _sut.ExecuteAsync(intent);

        // Verify the milestone-complete command was dispatched and completed
        _runLifecycleManager.Verify(r => r.RecordCommandAsync(
            "RUN-test-001", "spec", "milestone-complete", "dispatched", It.IsAny<CancellationToken>()), Times.Once);
        _runLifecycleManager.Verify(r => r.RecordCommandAsync(
            "RUN-test-001", "spec", "milestone-complete", "completed", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMilestoneComplete_DoesNotFallThroughToGenericRouter()
    {
        var intent = new WorkflowIntent
        {
            InputRaw = "/run",
            CorrelationId = "corr-milestone-007"
        };

        await _sut.ExecuteAsync(intent);

        // Verify the generic command router was NOT called
        _commandRouter.Verify(r => r.RouteAsync(It.IsAny<CommandRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMilestoneComplete_CursorSetClearsTaskAndStepState()
    {
        var intent = new WorkflowIntent
        {
            InputRaw = "/run",
            CorrelationId = "corr-milestone-008"
        };

        await _sut.ExecuteAsync(intent);

        // Find the cursor.set event and verify task/step identifiers are null
        var cursorSetEvent = _appendedEvents.First(e =>
            e.TryGetProperty("eventType", out var et) && et.GetString() == "cursor.set");

        var cursor = cursorSetEvent.GetProperty("cursor");
        cursor.GetProperty("taskId").ValueKind.Should().Be(JsonValueKind.Null, "taskId should be cleared on milestone completion");
        cursor.GetProperty("stepId").ValueKind.Should().Be(JsonValueKind.Null, "stepId should be cleared on milestone completion");
    }
}
