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
using ControlPlaneRiskLevel = nirmata.Agents.Execution.ControlPlane.RiskLevel;
using AgentValidationResult = nirmata.Agents.Execution.Validation.ValidationResult;
using RuntimeRoadmapContext = nirmata.Agents.Models.Runtime.RoadmapContext;

namespace nirmata.Agents.Tests.Integration.Orchestrator;

/// <summary>
/// Tests that the Orchestrator persists state/event updates deterministically
/// after every transition (Design Decision 6). Covers success, failure,
/// output-validation-failure, and exception paths.
/// </summary>
public class TransitionStatePersistenceTests
{
    private readonly Mock<IStateStore> _stateStore;
    private readonly Mock<IRunLifecycleManager> _runLifecycleManager;
    private readonly Mock<IGatingEngine> _gatingEngine;
    private readonly Mock<ICommandRouter> _commandRouter;
    private readonly Mock<IOutputValidator> _outputValidator;
    private readonly Mock<IPreflightValidator> _preflightValidator;
    private readonly List<JsonElement> _appendedEvents = new();

    private ExecutionOrchestrator BuildOrchestrator(
        Mock<IRoadmapper>? roadmapperOverride = null)
    {
        var workspace = new Mock<IWorkspace>();
        var specStore = new Mock<ISpecStore>();
        var validator = new Mock<IValidator>();
        var confirmationGate = new Mock<IConfirmationGate>();
        var prerequisiteValidator = new Mock<IPrerequisiteValidator>();
        var contextPackManager = new Mock<IContextPackManager>();

        // Default state
        _stateStore.Setup(s => s.ReadSnapshot()).Returns(new StateSnapshot
        {
            SchemaVersion = 1,
            Cursor = new StateCursor
            {
                MilestoneId = "MS-0001",
                PhaseId = "PH-0001",
                MilestoneStatus = "active",
                PhaseStatus = "active"
            }
        });

        // Capture appended events
        _stateStore
            .Setup(s => s.AppendEvent(It.IsAny<JsonElement>()))
            .Callback<JsonElement>(e => _appendedEvents.Add(e.Clone()));

        workspace.Setup(w => w.AosRootPath).Returns("/fake/.aos");

        prerequisiteValidator
            .Setup(v => v.EnsureWorkspaceInitializedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(PrerequisiteValidationResult.Satisfied("test"));
        prerequisiteValidator
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<GatingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PrerequisiteValidationResult.Satisfied("test"));

        _preflightValidator
            .Setup(v => v.ValidateAsync(It.IsAny<WorkflowIntent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentValidationResult());

        _outputValidator
            .Setup(v => v.ValidateAsync(It.IsAny<OrchestratorResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentValidationResult());

        _runLifecycleManager
            .Setup(r => r.StartRunAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunContext { RunId = "RUN-test-persist" });
        _runLifecycleManager
            .Setup(r => r.AttachInputAsync(It.IsAny<string>(), It.IsAny<WorkflowIntent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _runLifecycleManager
            .Setup(r => r.RecordCommandAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _runLifecycleManager
            .Setup(r => r.FinishRunAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var roadmapper = roadmapperOverride ?? new Mock<IRoadmapper>();
        if (roadmapperOverride == null)
        {
            roadmapper
                .Setup(x => x.GenerateRoadmapAsync(It.IsAny<RuntimeRoadmapContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new nirmata.Agents.Models.Results.RoadmapResult { IsSuccess = true });
        }

        var interviewerHandler = new InterviewerHandler(
            Mock.Of<INewProjectInterviewer>(),
            Mock.Of<IInterviewEvidenceWriter>());
        var responderHandler = new ResponderHandler(Mock.Of<IChatResponder>());
        var roadmapperHandler = new RoadmapperHandler(
            roadmapper.Object,
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

        return new ExecutionOrchestrator(
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
            _preflightValidator.Object,
            prerequisiteValidator.Object,
            _outputValidator.Object,
            contextPackManager.Object,
            responderHandler,
            inputClassifier,
            chatResponder,
            readOnlyHandler,
            Mock.Of<IHistoryWriter>(),
            Mock.Of<IHandoffStateStore>());
    }

    public TransitionStatePersistenceTests()
    {
        _stateStore = new Mock<IStateStore>();
        _runLifecycleManager = new Mock<IRunLifecycleManager>();
        _gatingEngine = new Mock<IGatingEngine>();
        _commandRouter = new Mock<ICommandRouter>();
        _outputValidator = new Mock<IOutputValidator>();
        _preflightValidator = new Mock<IPreflightValidator>();
    }

    private void SetupGatingResult(string targetPhase)
    {
        _gatingEngine
            .Setup(g => g.EvaluateAsync(It.IsAny<GatingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatingResult
            {
                TargetPhase = targetPhase,
                Reason = $"Routing to {targetPhase}",
                Reasoning = $"Test routing to {targetPhase}",
                RequiresConfirmation = false,
                ProposedAction = new ControlPlaneProposedAction
                {
                    Phase = targetPhase,
                    Description = $"Test {targetPhase}",
                    RiskLevel = ControlPlaneRiskLevel.WriteSafe,
                    SideEffects = Array.Empty<string>(),
                    AffectedResources = Array.Empty<string>()
                }
            });
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulDispatch_AppendsTransitionCompletedEvent()
    {
        // Arrange
        SetupGatingResult("Roadmapper");
        var sut = BuildOrchestrator();

        var intent = new WorkflowIntent
        {
            InputRaw = "/run create roadmap",
            CorrelationId = "corr-persist-001"
        };

        // Act
        var result = await sut.ExecuteAsync(intent);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var transitionEvent = _appendedEvents.FirstOrDefault(e =>
            e.TryGetProperty("eventType", out var et) &&
            et.GetString() == "orchestrator.transition.completed");

        transitionEvent.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "orchestrator.transition.completed event should be appended after successful dispatch");

        transitionEvent.GetProperty("targetPhase").GetString().Should().Be("Roadmapper");
        transitionEvent.GetProperty("runId").GetString().Should().Be("RUN-test-persist");
        transitionEvent.GetProperty("success").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulDispatch_ReconcileStateAfterTransition()
    {
        // Arrange
        SetupGatingResult("Roadmapper");
        var sut = BuildOrchestrator();

        var intent = new WorkflowIntent
        {
            InputRaw = "/run create roadmap",
            CorrelationId = "corr-persist-002"
        };

        // Act
        await sut.ExecuteAsync(intent);

        // Assert — EnsureWorkspaceInitialized is called to reconcile state.json
        // It's called at least once for prerequisite validation and once for transition reconciliation
        _stateStore.Verify(s => s.EnsureWorkspaceInitialized(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulDispatch_EventContainsTimestamp()
    {
        // Arrange
        SetupGatingResult("Roadmapper");
        var sut = BuildOrchestrator();

        var intent = new WorkflowIntent
        {
            InputRaw = "/run create roadmap",
            CorrelationId = "corr-persist-003"
        };

        // Act
        await sut.ExecuteAsync(intent);

        // Assert
        var transitionEvent = _appendedEvents.First(e =>
            e.TryGetProperty("eventType", out var et) &&
            et.GetString() == "orchestrator.transition.completed");

        transitionEvent.TryGetProperty("timestamp", out var ts).Should().BeTrue();
        ts.GetString().Should().NotBeNullOrEmpty();

        // Verify it's a valid ISO 8601 timestamp
        DateTimeOffset.TryParse(ts.GetString(), out _).Should().BeTrue(
            "timestamp should be a valid ISO 8601 date");
    }

    [Fact]
    public async Task ExecuteAsync_FailedDispatch_AppendsTransitionCompletedEventWithFailureFlag()
    {
        // Arrange — use a failing roadmapper
        SetupGatingResult("Roadmapper");
        var failingRoadmapper = new Mock<IRoadmapper>();
        failingRoadmapper
            .Setup(x => x.GenerateRoadmapAsync(It.IsAny<RuntimeRoadmapContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new nirmata.Agents.Models.Results.RoadmapResult { IsSuccess = false, Error = "roadmap generation failed" });

        var sut = BuildOrchestrator(roadmapperOverride: failingRoadmapper);

        var intent = new WorkflowIntent
        {
            InputRaw = "/run create roadmap",
            CorrelationId = "corr-persist-004"
        };

        // Act
        var result = await sut.ExecuteAsync(intent);

        // Assert
        result.IsSuccess.Should().BeFalse();

        // The transition event should record success=false since the dispatch failed
        var transitionEvent = _appendedEvents.FirstOrDefault(e =>
            e.TryGetProperty("eventType", out var et) &&
            (et.GetString() == "orchestrator.transition.completed" || et.GetString() == "orchestrator.transition.failed"));

        transitionEvent.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "a transition event should be appended even when dispatch fails");

        transitionEvent.GetProperty("success").GetBoolean().Should().BeFalse();
        transitionEvent.GetProperty("targetPhase").GetString().Should().Be("Roadmapper");
    }

    [Fact]
    public async Task ExecuteAsync_OutputValidationFailure_AppendsTransitionFailedEvent()
    {
        // Arrange — output validator fails (set up AFTER BuildOrchestrator to override default)
        SetupGatingResult("Roadmapper");
        var sut = BuildOrchestrator();

        _outputValidator
            .Setup(v => v.ValidateAsync(It.IsAny<OrchestratorResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentValidationResult
            {
                Issues = new[]
                {
                    new nirmata.Agents.Execution.Validation.ValidationIssue
                    {
                        IssueType = "OutputTestFailure",
                        Message = "Output check failed",
                        Severity = nirmata.Agents.Execution.Validation.ValidationSeverity.Error
                    }
                }
            });

        var intent = new WorkflowIntent
        {
            InputRaw = "/run create roadmap",
            CorrelationId = "corr-persist-005"
        };

        // Act
        var result = await sut.ExecuteAsync(intent);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FinalPhase.Should().Be("OutputValidation");

        var transitionEvent = _appendedEvents.FirstOrDefault(e =>
            e.TryGetProperty("eventType", out var et) &&
            et.GetString() == "orchestrator.transition.failed");

        transitionEvent.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "orchestrator.transition.failed event should be appended on output validation failure");

        transitionEvent.GetProperty("targetPhase").GetString().Should().Be("Roadmapper");
        transitionEvent.GetProperty("success").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_TransitionEvent_AppendsBeforeRunIsClosed()
    {
        // Arrange (set up callbacks AFTER BuildOrchestrator to override defaults)
        SetupGatingResult("Roadmapper");
        var sut = BuildOrchestrator();

        var callOrder = new List<string>();

        _stateStore
            .Setup(s => s.AppendEvent(It.IsAny<JsonElement>()))
            .Callback<JsonElement>(e =>
            {
                _appendedEvents.Add(e.Clone());
                if (e.TryGetProperty("eventType", out var et) &&
                    et.GetString()?.StartsWith("orchestrator.transition") == true)
                {
                    callOrder.Add("AppendEvent");
                }
            });

        _runLifecycleManager
            .Setup(r => r.FinishRunAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("FinishRun"))
            .Returns(Task.CompletedTask);

        var intent = new WorkflowIntent
        {
            InputRaw = "/run create roadmap",
            CorrelationId = "corr-persist-006"
        };

        // Act
        await sut.ExecuteAsync(intent);

        // Assert — transition event must be appended before the run is closed
        callOrder.Should().ContainInOrder("AppendEvent", "FinishRun");
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulDispatch_EventIncludesCommandMetadata()
    {
        // Arrange
        SetupGatingResult("Roadmapper");
        var sut = BuildOrchestrator();

        var intent = new WorkflowIntent
        {
            InputRaw = "/run create roadmap",
            CorrelationId = "corr-persist-007"
        };

        // Act
        await sut.ExecuteAsync(intent);

        // Assert
        var transitionEvent = _appendedEvents.First(e =>
            e.TryGetProperty("eventType", out var et) &&
            et.GetString() == "orchestrator.transition.completed");

        // The Roadmapper dispatch includes commandGroup and command in artifacts
        transitionEvent.TryGetProperty("commandGroup", out var cg).Should().BeTrue();
        cg.GetString().Should().Be("spec");

        transitionEvent.TryGetProperty("command", out var cmd).Should().BeTrue();
        cmd.GetString().Should().Be("roadmap");
    }

    [Fact]
    public async Task ExecuteAsync_ExceptionDuringDispatch_AppendsTransitionFailedEvent()
    {
        // Arrange — roadmapper throws an exception
        SetupGatingResult("Roadmapper");
        var throwingRoadmapper = new Mock<IRoadmapper>();
        throwingRoadmapper
            .Setup(x => x.GenerateRoadmapAsync(It.IsAny<RuntimeRoadmapContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected roadmapper error"));

        var sut = BuildOrchestrator(roadmapperOverride: throwingRoadmapper);

        var intent = new WorkflowIntent
        {
            InputRaw = "/run create roadmap",
            CorrelationId = "corr-persist-008"
        };

        // Act
        var result = await sut.ExecuteAsync(intent);

        // Assert
        result.IsSuccess.Should().BeFalse();

        var transitionEvent = _appendedEvents.FirstOrDefault(e =>
            e.TryGetProperty("eventType", out var et) &&
            et.GetString() == "orchestrator.transition.failed");

        transitionEvent.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "orchestrator.transition.failed event should be appended on exception");

        transitionEvent.GetProperty("targetPhase").GetString().Should().Be("Roadmapper");
        transitionEvent.GetProperty("success").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulDispatch_RunIsClosedAfterTransitionPersistence()
    {
        // Arrange — verify FinishRunAsync is called after transition state is persisted
        SetupGatingResult("Roadmapper");
        var sut = BuildOrchestrator();

        var intent = new WorkflowIntent
        {
            InputRaw = "/run create roadmap",
            CorrelationId = "corr-persist-009"
        };

        // Act
        await sut.ExecuteAsync(intent);

        // Assert — the run is closed after transition state is persisted
        _runLifecycleManager.Verify(r => r.FinishRunAsync(
            It.IsAny<string>(), It.IsAny<bool>(),
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // State reconciliation must have occurred
        _stateStore.Verify(s => s.EnsureWorkspaceInitialized(), Times.AtLeastOnce);
    }
}
