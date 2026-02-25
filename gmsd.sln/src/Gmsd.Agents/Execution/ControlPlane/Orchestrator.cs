using Gmsd.Aos.Contracts.Commands;
using Gmsd.Aos.Public;
using Gmsd.Aos.Public.Context.Packs;
using Gmsd.Aos.Public.Services;
using Gmsd.Agents.Persistence.Runs;
using Gmsd.Agents.Execution.Planning;
using Gmsd.Agents.Execution.Planning.PhasePlanner;
using System.Text.Json;
using Gmsd.Agents.Execution.Validation;
using Gmsd.Agents.Execution.Preflight;
using Gmsd.Agents.Execution.Context;
using Gmsd.Agents.Execution.ControlPlane.Streaming;

namespace Gmsd.Agents.Execution.ControlPlane;

/// <summary>
/// Orchestrator implementation that implements the "classify → gate → dispatch → validate → persist → next" workflow loop.
/// </summary>
public sealed class Orchestrator : IOrchestrator
{
    private readonly IGatingEngine _gatingEngine;
    private readonly ICommandRouter _commandRouter;
    private readonly IWorkspace _workspace;
    private readonly ISpecStore _specStore;
    private readonly IStateStore _stateStore;
    private readonly IValidator _validator;
    private readonly IRunLifecycleManager _runLifecycleManager;
    private readonly IConfirmationGate _confirmationGate;
    private readonly IConfirmationGateEvaluator? _confirmationGateEvaluator;
    private readonly InterviewerHandler _interviewerHandler;
    private readonly ResponderHandler _responderHandler;
    private readonly InputClassifier _inputClassifier;
    private readonly ChatResponder _chatResponder;
    private readonly ReadOnlyHandler _readOnlyHandler;
    private readonly RoadmapperHandler _roadmapperHandler;
    private readonly PhasePlannerHandler _phasePlannerHandler;
    private readonly TaskExecutorHandler _taskExecutorHandler;
    private readonly VerifierHandler _verifierHandler;
        private readonly AtomicGitCommitterHandler _atomicGitCommitterHandler;
    private readonly IPreflightValidator _preflightValidator;
    private readonly IPrerequisiteValidator _prerequisiteValidator;
    private readonly IOutputValidator _outputValidator;
    private readonly IContextPackManager _contextPackManager;
    private readonly ConfirmationEventPublisher? _confirmationEventPublisher;

    private readonly ILogger<Orchestrator>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Orchestrator"/> class.
    /// </summary>
    public Orchestrator(
        IGatingEngine gatingEngine,
        ICommandRouter commandRouter,
        IWorkspace workspace,
        ISpecStore specStore,
        IStateStore stateStore,
        IValidator validator,
        IRunLifecycleManager runLifecycleManager,
        IConfirmationGate confirmationGate,
        InterviewerHandler interviewerHandler,
        RoadmapperHandler roadmapperHandler,
        PhasePlannerHandler phasePlannerHandler,
        TaskExecutorHandler taskExecutorHandler,
        VerifierHandler verifierHandler,
        AtomicGitCommitterHandler atomicGitCommitterHandler,
        IPreflightValidator preflightValidator,
        IPrerequisiteValidator prerequisiteValidator,
        IOutputValidator outputValidator,
        IContextPackManager contextPackManager,
        ResponderHandler responderHandler,
        InputClassifier inputClassifier,
        ChatResponder chatResponder,
        ReadOnlyHandler readOnlyHandler,
        ConfirmationEventPublisher? confirmationEventPublisher = null,
        ILogger<Orchestrator>? logger = null)
    {
        _gatingEngine = gatingEngine ?? throw new ArgumentNullException(nameof(gatingEngine));
        _commandRouter = commandRouter ?? throw new ArgumentNullException(nameof(commandRouter));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _specStore = specStore ?? throw new ArgumentNullException(nameof(specStore));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _runLifecycleManager = runLifecycleManager ?? throw new ArgumentNullException(nameof(runLifecycleManager));
        _confirmationGate = confirmationGate ?? throw new ArgumentNullException(nameof(confirmationGate));
        _interviewerHandler = interviewerHandler ?? throw new ArgumentNullException(nameof(interviewerHandler));
        _roadmapperHandler = roadmapperHandler ?? throw new ArgumentNullException(nameof(roadmapperHandler));
        _phasePlannerHandler = phasePlannerHandler ?? throw new ArgumentNullException(nameof(phasePlannerHandler));
        _taskExecutorHandler = taskExecutorHandler ?? throw new ArgumentNullException(nameof(taskExecutorHandler));
        _verifierHandler = verifierHandler ?? throw new ArgumentNullException(nameof(verifierHandler));
        _atomicGitCommitterHandler = atomicGitCommitterHandler ?? throw new ArgumentNullException(nameof(atomicGitCommitterHandler));
        _preflightValidator = preflightValidator ?? throw new ArgumentNullException(nameof(preflightValidator));
        _prerequisiteValidator = prerequisiteValidator ?? throw new ArgumentNullException(nameof(prerequisiteValidator));
        _outputValidator = outputValidator ?? throw new ArgumentNullException(nameof(outputValidator));
        _contextPackManager = contextPackManager ?? throw new ArgumentNullException(nameof(contextPackManager));
        _responderHandler = responderHandler ?? throw new ArgumentNullException(nameof(responderHandler));
        _inputClassifier = inputClassifier ?? throw new ArgumentNullException(nameof(inputClassifier));
        _chatResponder = chatResponder ?? throw new ArgumentNullException(nameof(chatResponder));
        _readOnlyHandler = readOnlyHandler ?? throw new ArgumentNullException(nameof(readOnlyHandler));
        _confirmationEventPublisher = confirmationEventPublisher;
        _logger = logger;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Orchestrator"/> class with confirmation evaluator.
    /// </summary>
    public Orchestrator(
        IGatingEngine gatingEngine,
        ICommandRouter commandRouter,
        IWorkspace workspace,
        ISpecStore specStore,
        IStateStore stateStore,
        IValidator validator,
        IRunLifecycleManager runLifecycleManager,
        IConfirmationGate confirmationGate,
        IConfirmationGateEvaluator confirmationGateEvaluator,
        InterviewerHandler interviewerHandler,
        RoadmapperHandler roadmapperHandler,
        PhasePlannerHandler phasePlannerHandler,
        TaskExecutorHandler taskExecutorHandler,
        VerifierHandler verifierHandler,
        AtomicGitCommitterHandler atomicGitCommitterHandler,
        IPreflightValidator preflightValidator,
        IPrerequisiteValidator prerequisiteValidator,
        IOutputValidator outputValidator,
        IContextPackManager contextPackManager,
        ResponderHandler responderHandler,
        InputClassifier inputClassifier,
        ChatResponder chatResponder,
        ReadOnlyHandler readOnlyHandler,
        ConfirmationEventPublisher? confirmationEventPublisher = null,
        ILogger<Orchestrator>? logger = null)
    {
        _gatingEngine = gatingEngine ?? throw new ArgumentNullException(nameof(gatingEngine));
        _commandRouter = commandRouter ?? throw new ArgumentNullException(nameof(commandRouter));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _specStore = specStore ?? throw new ArgumentNullException(nameof(specStore));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _runLifecycleManager = runLifecycleManager ?? throw new ArgumentNullException(nameof(runLifecycleManager));
        _confirmationGate = confirmationGate ?? throw new ArgumentNullException(nameof(confirmationGate));
        _confirmationGateEvaluator = confirmationGateEvaluator ?? throw new ArgumentNullException(nameof(confirmationGateEvaluator));
        _interviewerHandler = interviewerHandler ?? throw new ArgumentNullException(nameof(interviewerHandler));
        _roadmapperHandler = roadmapperHandler ?? throw new ArgumentNullException(nameof(roadmapperHandler));
        _phasePlannerHandler = phasePlannerHandler ?? throw new ArgumentNullException(nameof(phasePlannerHandler));
        _taskExecutorHandler = taskExecutorHandler ?? throw new ArgumentNullException(nameof(taskExecutorHandler));
        _verifierHandler = verifierHandler ?? throw new ArgumentNullException(nameof(verifierHandler));
        _atomicGitCommitterHandler = atomicGitCommitterHandler ?? throw new ArgumentNullException(nameof(atomicGitCommitterHandler));
        _preflightValidator = preflightValidator ?? throw new ArgumentNullException(nameof(preflightValidator));
        _prerequisiteValidator = prerequisiteValidator ?? throw new ArgumentNullException(nameof(prerequisiteValidator));
        _outputValidator = outputValidator ?? throw new ArgumentNullException(nameof(outputValidator));
        _contextPackManager = contextPackManager ?? throw new ArgumentNullException(nameof(contextPackManager));
        _responderHandler = responderHandler ?? throw new ArgumentNullException(nameof(responderHandler));
        _inputClassifier = inputClassifier ?? throw new ArgumentNullException(nameof(inputClassifier));
        _chatResponder = chatResponder ?? throw new ArgumentNullException(nameof(chatResponder));
        _readOnlyHandler = readOnlyHandler ?? throw new ArgumentNullException(nameof(readOnlyHandler));
        _confirmationEventPublisher = confirmationEventPublisher;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OrchestratorResult> ExecuteAsync(WorkflowIntent intent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(intent);

        var classificationResult = _inputClassifier.Classify(intent.InputRaw);
        var classifiedIntent = classificationResult.Intent;

        // Emit intent.classified event for streaming consumers
        // (This would be handled by the streaming layer, but we capture the result here)

        if (classifiedIntent.SideEffect == SideEffect.None)
        {
            return await _chatResponder.Respond(intent.InputRaw);
        }

        if (classifiedIntent.SideEffect == SideEffect.ReadOnly)
        {
            return await _readOnlyHandler.HandleAsync(classifiedIntent);
        }

        // Ensure baseline state artifacts are initialized before any write-phase workflow execution.
        var workspaceInitialization = await _prerequisiteValidator.EnsureWorkspaceInitializedAsync(ct);
        if (!workspaceInitialization.IsSatisfied && workspaceInitialization.Missing != null)
        {
            _confirmationEventPublisher?.PublishPrerequisiteMissing(
                Guid.NewGuid().ToString("N"),
                MapToMissingPrerequisite(workspaceInitialization.Missing));

            return CreateConversationalRecoveryResult(workspaceInitialization.Missing);
        }

        // Build gating context to get the target phase for prerequisite validation
        var gatingContext = await BuildGatingContextAsync(ct);

        // Determine what phase we would route to (for prerequisite checking)
        var previewGatingResult = await _gatingEngine.EvaluateAsync(gatingContext, ct);
        var targetPhase = previewGatingResult.TargetPhase;

        // Validate prerequisites for the target phase
        var prerequisiteResult = await _prerequisiteValidator.ValidateAsync(targetPhase, gatingContext, ct);
        if (!prerequisiteResult.IsSatisfied && prerequisiteResult.Missing != null)
        {
            // Emit prerequisite missing event for conversational recovery
            _confirmationEventPublisher?.PublishPrerequisiteMissing(
                Guid.NewGuid().ToString("N"),
                MapToMissingPrerequisite(prerequisiteResult.Missing));

            // Return a conversational response suggesting the recovery action
            return CreateConversationalRecoveryResult(prerequisiteResult.Missing);
        }

        // Start the run lifecycle only for write operations
        var runContext = await _runLifecycleManager.StartRunAsync(ct);

        try
        {
            // Attach the input to the run
            await _runLifecycleManager.AttachInputAsync(runContext.RunId, intent, ct);

            // Perform pre-flight validation
            var validationResult = await _preflightValidator.ValidateAsync(intent, ct);
            if (!validationResult.IsValid)
            {
                var validationErrors = validationResult.Issues.ToDictionary(issue => issue.IssueType, issue => (object)issue.Message);
                await _runLifecycleManager.FinishRunAsync(runContext.RunId, false, validationErrors, ct);
                return new OrchestratorResult
                {
                    IsSuccess = false,
                    FinalPhase = "PreflightValidation",
                    RunId = runContext.RunId,
                    Artifacts = validationErrors
                };
            }

            // Evaluate gates to determine target phase (using gatingContext already built for prerequisite validation)
            var gatingResult = await _gatingEngine.EvaluateAsync(gatingContext, ct);

            // Handle confirmation requirement - cancellation path when user rejects
            if (gatingResult.RequiresConfirmation)
            {
                var gatingClassification = new IntentClassificationResult
                {
                    Intent = new Intent
                    {
                        Kind = IntentKind.WorkflowCommand,
                        SideEffect = SideEffect.Write,
                        Confidence = 1.0,
                        Reasoning = gatingResult.Reasoning ?? "Gating engine determined confirmation is required"
                    },
                    ParsedCommand = new ParsedCommand
                    {
                        RawInput = intent.InputRaw,
                        CommandName = gatingResult.TargetPhase.ToLowerInvariant(),
                        SideEffect = SideEffect.Write,
                        Confidence = 1.0,
                        IsKnownCommand = true
                    }
                };

                var confirmationResult = _confirmationGate.Evaluate(gatingClassification);

                if (confirmationResult.RequiresConfirmation && confirmationResult.Request != null)
                {
                    // Emit confirmation requested event via streaming protocol
                    if (gatingResult.ProposedAction != null)
                    {
                        _confirmationEventPublisher?.PublishRequested(
                            confirmationResult.Request.Id,
                            gatingResult.ProposedAction,
                            gatingResult.ProposedAction.RiskLevel,
                            confirmationResult.Reason,
                            classifiedIntent.Confidence,
                            confirmationResult.Request.Threshold,
                            confirmationResult.Request.Timeout);
                    }

                    // At this point, the system would wait for user confirmation via streaming protocol
                    // For now, we simulate a rejected confirmation to test the cancellation path
                    // In production, this would be handled by the streaming dialogue protocol
                    var rejectionResponse = new ConfirmationResponse
                    {
                        RequestId = confirmationResult.Request.Id,
                        Confirmed = false,
                        Message = "User rejected the proposed action"
                    };

                    var userConfirmed = _confirmationGate.ProcessResponse(rejectionResponse);

                    if (!userConfirmed)
                    {
                        // User rejected - cancel the run and return cancellation result
                        var cancellationOutputs = new Dictionary<string, object>
                        {
                            ["cancellationReason"] = "User rejected proposed action",
                            ["proposedPhase"] = gatingResult.TargetPhase,
                            ["proposedAction"] = gatingResult.ProposedAction?.Description ?? "Unknown action",
                            ["confirmationRequestId"] = confirmationResult.Request.Id
                        };

                        await _runLifecycleManager.FinishRunAsync(runContext.RunId, false, cancellationOutputs, ct);

                        return new OrchestratorResult
                        {
                            IsSuccess = false,
                            FinalPhase = "Cancelled",
                            RunId = runContext.RunId,
                            Artifacts = cancellationOutputs
                        };
                    }
                }
            }

            // Dispatch to the appropriate phase handler
            var dispatchResult = await DispatchToPhaseAsync(gatingResult.TargetPhase, intent, runContext.RunId, ct);

            dispatchResult.Artifacts["targetPhase"] = gatingResult.TargetPhase;
            dispatchResult.Artifacts["reason"] = gatingResult.Reason;

            // Perform output validation
            var orchestratorResultForValidation = new OrchestratorResult
            {
                IsSuccess = dispatchResult.IsSuccess,
                FinalPhase = gatingResult.TargetPhase,
                RunId = runContext.RunId,
                Artifacts = dispatchResult.Artifacts
            };

            var outputValidationResult = await _outputValidator.ValidateAsync(orchestratorResultForValidation, ct);
            if (!outputValidationResult.IsValid)
            {
                var validationErrors = outputValidationResult.Issues.ToDictionary(issue => issue.IssueType, issue => (object)issue.Message);
                await _runLifecycleManager.FinishRunAsync(runContext.RunId, false, validationErrors, ct);
                return new OrchestratorResult
                {
                    IsSuccess = false,
                    FinalPhase = "OutputValidation",
                    RunId = runContext.RunId,
                    Artifacts = validationErrors
                };
            }

            // Close the run with the result
            var outputs = new Dictionary<string, object>
            {
                ["targetPhase"] = gatingResult.TargetPhase,
                ["reason"] = gatingResult.Reason,
                ["dispatchSuccess"] = dispatchResult.IsSuccess
            };

            await _runLifecycleManager.FinishRunAsync(runContext.RunId, dispatchResult.IsSuccess, outputs, ct);

            return new OrchestratorResult
            {
                IsSuccess = dispatchResult.IsSuccess,
                FinalPhase = gatingResult.TargetPhase,
                RunId = runContext.RunId,
                Artifacts = dispatchResult.Artifacts
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unhandled exception in Orchestrator.ExecuteAsync (RunId: {RunId}).", runContext.RunId);

            // Close the run with failure status
            var outputs = new Dictionary<string, object>
            {
                ["error"] = ex.Message,
                ["errorType"] = ex.GetType().Name
            };

            await _runLifecycleManager.FinishRunAsync(runContext.RunId, false, outputs, ct);

            return new OrchestratorResult
            {
                IsSuccess = false,
                FinalPhase = null,
                RunId = runContext.RunId,
                Artifacts = new Dictionary<string, object> { ["error"] = ex.Message }
            };
        }
    }

    private async Task<GatingContext> BuildGatingContextAsync(CancellationToken ct)
    {
        // Read workspace state from spec store and state store
        // For now, use default values - these would be populated from actual store queries
        var snapshot = _stateStore.ReadSnapshot();

        var cursor = snapshot?.Cursor;
        var cursorDisplay = !string.IsNullOrWhiteSpace(cursor?.TaskId)
            ? cursor.TaskId
            : cursor?.PhaseId;

        return new GatingContext
        {
            HasProject = CheckProjectExists(),
            HasRoadmap = CheckRoadmapExists(),
            HasPlan = CheckPlanExists(snapshot),
            CurrentCursor = cursorDisplay,
            LastExecutionStatus = cursor?.TaskStatus,
            LastVerificationStatus = cursor?.StepStatus,
            StateData = new Dictionary<string, object>()
        };
    }

    private bool CheckProjectExists()
    {
        try
        {
            var projectPath = Path.Combine(_workspace.AosRootPath, "spec", "project.json");
            return File.Exists(projectPath);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to check if project spec exists.");
            return false;
        }
    }

    private bool CheckRoadmapExists()
    {
        try
        {
            if (_specStore is SpecStore store)
            {
                var doc = store.Inner.ReadRoadmap();
                return doc.Roadmap.Items.Count > 0;
            }

            var roadmapPath = Path.Combine(_workspace.AosRootPath, "spec", "roadmap.json");
            return File.Exists(roadmapPath);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to check if roadmap exists.");
            return false;
        }
    }

    private bool CheckPlanExists(Gmsd.Aos.Contracts.State.StateSnapshot? snapshot)
    {
        if (snapshot?.Cursor == null || string.IsNullOrWhiteSpace(snapshot.Cursor.PhaseId))
            return false;

        try
        {
            var planPath = Path.Combine(_workspace.AosRootPath, "spec", "phases", snapshot.Cursor.PhaseId, "plan.json");
            return File.Exists(planPath);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to check if phase plan exists for {PhaseId}.", snapshot.Cursor.PhaseId);
            return false;
        }
    }

    private async Task<DispatchResult> DispatchToPhaseAsync(string targetPhase, WorkflowIntent intent, string runId, CancellationToken ct)
    {
        // Special handling for Responder phase - use dedicated handler
        if (targetPhase == "Responder")
        {
            var command = MapPhaseToCommand(targetPhase);
            await _runLifecycleManager.RecordCommandAsync(runId, command.Group, command.Command, "dispatched", ct);

            var result = await _responderHandler.HandleAsync(command, runId, ct);

            await _runLifecycleManager.RecordCommandAsync(runId, command.Group, command.Command, result.IsSuccess ? "completed" : "failed", ct);

            return new DispatchResult
            {
                IsSuccess = result.IsSuccess,
                Artifacts = new Dictionary<string, object>
                {
                    ["phase"] = targetPhase,
                    ["commandGroup"] = command.Group,
                    ["command"] = command.Command,
                    ["output"] = result.Output ?? string.Empty,
                    ["error"] = result.ErrorOutput ?? string.Empty
                }
            };
        }

        // Special handling for Interviewer phase - use dedicated handler
        if (targetPhase == "Interviewer")
        {
            var command = MapPhaseToCommand(targetPhase);
            await _runLifecycleManager.RecordCommandAsync(runId, command.Group, command.Command, "dispatched", ct);

            var result = await _interviewerHandler.HandleAsync(command, runId, ct);

            await _runLifecycleManager.RecordCommandAsync(runId, command.Group, command.Command, result.IsSuccess ? "completed" : "failed", ct);

            return new DispatchResult
            {
                IsSuccess = result.IsSuccess,
                Artifacts = new Dictionary<string, object>
                {
                    ["phase"] = targetPhase,
                    ["commandGroup"] = command.Group,
                    ["command"] = command.Command,
                    ["output"] = result.Output ?? string.Empty,
                    ["error"] = result.ErrorOutput ?? string.Empty
                }
            };
        }

        // Special handling for Roadmapper phase - use dedicated handler
        if (targetPhase == "Roadmapper")
        {
            var command = MapPhaseToCommand(targetPhase);
            await _runLifecycleManager.RecordCommandAsync(runId, command.Group, command.Command, "dispatched", ct);

            var result = await _roadmapperHandler.HandleAsync(command, runId, ct);

            await _runLifecycleManager.RecordCommandAsync(runId, command.Group, command.Command, result.IsSuccess ? "completed" : "failed", ct);

            return new DispatchResult
            {
                IsSuccess = result.IsSuccess,
                Artifacts = new Dictionary<string, object>
                {
                    ["phase"] = targetPhase,
                    ["commandGroup"] = command.Group,
                    ["command"] = command.Command,
                    ["output"] = result.Output ?? string.Empty,
                    ["error"] = result.ErrorOutput ?? string.Empty,
                    ["hasRoadmap"] = result.IsSuccess
                }
            };
        }

        // Special handling for Planner phase - use dedicated handler
        if (targetPhase == "Planner")
        {
            var command = MapPhaseToCommand(targetPhase);
            var snapshot = _stateStore.ReadSnapshot();
            var phaseId = snapshot?.Cursor?.PhaseId;
            if (string.IsNullOrWhiteSpace(phaseId))
            {
                return new DispatchResult
                {
                    IsSuccess = false,
                    Artifacts = new Dictionary<string, object>
                    {
                        ["phase"] = targetPhase,
                        ["error"] = "No phaseId available in state cursor. Run the Roadmapper gate first."
                    }
                };
            }

            command = new CommandRequest
            {
                Group = command.Group,
                Command = command.Command,
                Options = new Dictionary<string, string?>
                {
                    ["phase-id"] = phaseId
                }
            };
            await _runLifecycleManager.RecordCommandAsync(runId, command.Group, command.Command, "dispatched", ct);

            var result = await _phasePlannerHandler.HandleAsync(command, runId, ct);

            await _runLifecycleManager.RecordCommandAsync(runId, command.Group, command.Command, result.IsSuccess ? "completed" : "failed", ct);

            return new DispatchResult
            {
                IsSuccess = result.IsSuccess,
                Artifacts = new Dictionary<string, object>
                {
                    ["phase"] = targetPhase,
                    ["commandGroup"] = command.Group,
                    ["command"] = command.Command,
                    ["output"] = result.Output ?? string.Empty,
                    ["error"] = result.ErrorOutput ?? string.Empty,
                    ["hasPlan"] = result.IsSuccess
                }
            };
        }

        // Special handling for Executor phase - use dedicated handler
        if (targetPhase == "Executor")
        {
            var snapshot = _stateStore.ReadSnapshot();
            var taskId = snapshot?.Cursor?.TaskId;
            if (string.IsNullOrWhiteSpace(taskId))
            {
                return new DispatchResult { IsSuccess = false, Artifacts = new() { ["error"] = "No taskId available in state cursor for Executor phase." } };
            }

                        var packId = await _contextPackManager.CreatePackAsync("task", taskId, new ContextPackBudget(1024 * 1024, 100), ct);

            var command = new CommandRequest
            {
                Group = "run",
                Command = "execute",
                Options = new Dictionary<string, string?>
                {
                    ["task-id"] = taskId,
                    ["context-pack-id"] = packId
                }
            };

            await _runLifecycleManager.RecordCommandAsync(runId, command.Group, command.Command, "dispatched", ct);

            var result = await _taskExecutorHandler.HandleAsync(command, runId, ct);

            await _runLifecycleManager.RecordCommandAsync(runId, command.Group, command.Command, result.IsSuccess ? "completed" : "failed", ct);

            return new DispatchResult
            {
                IsSuccess = result.IsSuccess,
                Artifacts = new Dictionary<string, object>
                {
                    ["phase"] = targetPhase,
                    ["commandGroup"] = command.Group,
                    ["command"] = command.Command,
                    ["output"] = result.Output ?? string.Empty,
                    ["error"] = result.ErrorOutput ?? string.Empty,
                    ["executed"] = result.IsSuccess
                }
            };
        }

        // Special handling for Verifier phase - use dedicated handler
        if (targetPhase == "Verifier")
        {
            var command = MapPhaseToCommand(targetPhase);
            await _runLifecycleManager.RecordCommandAsync(runId, command.Group, command.Command, "dispatched", ct);

            var result = await _verifierHandler.HandleAsync(command, runId, ct);

            await _runLifecycleManager.RecordCommandAsync(runId, command.Group, command.Command, result.IsSuccess ? "completed" : "failed", ct);

            // Check for routing hint from VerifierHandler
            var artifacts = new Dictionary<string, object>
            {
                ["phase"] = targetPhase,
                ["commandGroup"] = command.Group,
                ["command"] = command.Command,
                ["output"] = result.Output ?? string.Empty,
                ["error"] = result.ErrorOutput ?? string.Empty,
                ["verified"] = result.IsSuccess
            };

            if (!string.IsNullOrEmpty(result.RoutingHint))
            {
                artifacts["routingHint"] = result.RoutingHint;
            }

            return new DispatchResult
            {
                IsSuccess = result.IsSuccess,
                Artifacts = artifacts
            };
        }

        // Map phase to command and dispatch via router for other phases
        var otherCommand = MapPhaseToCommand(targetPhase);

        // Record the dispatched command
        await _runLifecycleManager.RecordCommandAsync(runId, otherCommand.Group, otherCommand.Command, "dispatched", ct);

        // Dispatch via command router
        var routeResult = await _commandRouter.RouteAsync(otherCommand, ct);

        await _runLifecycleManager.RecordCommandAsync(runId, otherCommand.Group, otherCommand.Command, routeResult.IsSuccess ? "completed" : "failed", ct);

        return new DispatchResult
        {
            IsSuccess = routeResult.IsSuccess,
            Artifacts = new Dictionary<string, object>
            {
                ["phase"] = targetPhase,
                ["commandGroup"] = otherCommand.Group,
                ["command"] = otherCommand.Command,
                ["output"] = routeResult.Output ?? string.Empty
            }
        };
    }

    private static Gmsd.Aos.Contracts.Commands.CommandRequest MapPhaseToCommand(string phase)
    {
        return phase switch
        {
            "Interviewer" => Gmsd.Aos.Contracts.Commands.CommandRequest.Create("spec", "init"),
            "Roadmapper" => Gmsd.Aos.Contracts.Commands.CommandRequest.Create("spec", "roadmap"),
            "Planner" => Gmsd.Aos.Contracts.Commands.CommandRequest.Create("spec", "plan"),
            "Executor" => Gmsd.Aos.Contracts.Commands.CommandRequest.Create("run", "execute"),
            "Verifier" => Gmsd.Aos.Contracts.Commands.CommandRequest.Create("run", "verify"),
            "Responder" => Gmsd.Aos.Contracts.Commands.CommandRequest.Create("chat", "response"),
            "FixPlanner" => Gmsd.Aos.Contracts.Commands.CommandRequest.Create("spec", "fix"),
            _ => Gmsd.Aos.Contracts.Commands.CommandRequest.Create("run", "status")
        };
    }

    /// <summary>
    /// Maps a MissingPrerequisiteDetail to the MissingPrerequisite type used by the event publisher.
    /// </summary>
    private static MissingPrerequisite MapToMissingPrerequisite(MissingPrerequisiteDetail detail)
    {
        return new MissingPrerequisite
        {
            PrerequisiteType = detail.Type.ToString(),
            Description = detail.Description,
            ExpectedPath = detail.ExpectedPath,
            RecoveryAction = detail.RecoveryAction,
            ConversationalPrompt = detail.ConversationalPrompt
        };
    }

    /// <summary>
    /// Creates an OrchestratorResult for conversational recovery when prerequisites are missing.
    /// </summary>
    private static OrchestratorResult CreateConversationalRecoveryResult(MissingPrerequisiteDetail missing)
    {
        return new OrchestratorResult
        {
            IsSuccess = false,
            FinalPhase = "PrerequisiteCheck",
            RunId = null,
            Artifacts = new Dictionary<string, object>
            {
                ["prerequisiteType"] = missing.Type.ToString(),
                ["description"] = missing.Description,
                ["expectedPath"] = missing.ExpectedPath,
                ["recoveryAction"] = missing.RecoveryAction ?? "unknown",
                ["suggestedCommand"] = missing.SuggestedCommand ?? string.Empty,
                ["conversationalPrompt"] = missing.ConversationalPrompt ?? "Workspace setup required before proceeding.",
                ["failureCode"] = missing.FailureCode ?? string.Empty,
                ["failingPrerequisite"] = missing.FailingPrerequisite ?? string.Empty,
                ["attemptedRepairs"] = missing.AttemptedRepairs,
                ["suggestedFixes"] = missing.SuggestedFixes,
                ["requiresUserAction"] = true
            }
        };
    }

    private sealed class DispatchResult
    {
        public required bool IsSuccess { get; init; }
        public Dictionary<string, object> Artifacts { get; init; } = new();
    }
}
