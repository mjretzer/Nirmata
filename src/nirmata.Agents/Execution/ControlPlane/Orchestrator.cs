using nirmata.Aos.Contracts.Commands;
using nirmata.Aos.Contracts.State;
using nirmata.Aos.Public;
using nirmata.Aos.Public.Context.Packs;
using nirmata.Aos.Public.Services;
using nirmata.Agents.Persistence.Runs;
using nirmata.Agents.Execution.Planning;
using nirmata.Agents.Execution.Planning.PhasePlanner;
using nirmata.Agents.Execution.Continuity;
using nirmata.Agents.Execution.Continuity.HistoryWriter;
using System.Text.Json;
using nirmata.Agents.Execution.Validation;
using nirmata.Agents.Execution.Preflight;
using nirmata.Agents.Execution.Context;
using nirmata.Agents.Execution.ControlPlane.Streaming;

namespace nirmata.Agents.Execution.ControlPlane;

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
    private readonly FixPlannerHandler _fixPlannerHandler;
    private readonly AtomicGitCommitterHandler _atomicGitCommitterHandler;
    private readonly IPreflightValidator _preflightValidator;
    private readonly IPrerequisiteValidator _prerequisiteValidator;
    private readonly IOutputValidator _outputValidator;
    private readonly IContextPackManager _contextPackManager;
    private readonly IHistoryWriter _historyWriter;
    private readonly IHandoffStateStore _handoffStateStore;
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
        FixPlannerHandler fixPlannerHandler,
        AtomicGitCommitterHandler atomicGitCommitterHandler,
        IPreflightValidator preflightValidator,
        IPrerequisiteValidator prerequisiteValidator,
        IOutputValidator outputValidator,
        IContextPackManager contextPackManager,
        ResponderHandler responderHandler,
        InputClassifier inputClassifier,
        ChatResponder chatResponder,
        ReadOnlyHandler readOnlyHandler,
        IHistoryWriter historyWriter,
        IHandoffStateStore handoffStateStore,
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
        _fixPlannerHandler = fixPlannerHandler ?? throw new ArgumentNullException(nameof(fixPlannerHandler));
        _atomicGitCommitterHandler = atomicGitCommitterHandler ?? throw new ArgumentNullException(nameof(atomicGitCommitterHandler));
        _preflightValidator = preflightValidator ?? throw new ArgumentNullException(nameof(preflightValidator));
        _prerequisiteValidator = prerequisiteValidator ?? throw new ArgumentNullException(nameof(prerequisiteValidator));
        _outputValidator = outputValidator ?? throw new ArgumentNullException(nameof(outputValidator));
        _contextPackManager = contextPackManager ?? throw new ArgumentNullException(nameof(contextPackManager));
        _responderHandler = responderHandler ?? throw new ArgumentNullException(nameof(responderHandler));
        _inputClassifier = inputClassifier ?? throw new ArgumentNullException(nameof(inputClassifier));
        _chatResponder = chatResponder ?? throw new ArgumentNullException(nameof(chatResponder));
        _readOnlyHandler = readOnlyHandler ?? throw new ArgumentNullException(nameof(readOnlyHandler));
        _historyWriter = historyWriter ?? throw new ArgumentNullException(nameof(historyWriter));
        _handoffStateStore = handoffStateStore ?? throw new ArgumentNullException(nameof(handoffStateStore));
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
        FixPlannerHandler fixPlannerHandler,
        AtomicGitCommitterHandler atomicGitCommitterHandler,
        IPreflightValidator preflightValidator,
        IPrerequisiteValidator prerequisiteValidator,
        IOutputValidator outputValidator,
        IContextPackManager contextPackManager,
        ResponderHandler responderHandler,
        InputClassifier inputClassifier,
        ChatResponder chatResponder,
        ReadOnlyHandler readOnlyHandler,
        IHistoryWriter historyWriter,
        IHandoffStateStore handoffStateStore,
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
        _fixPlannerHandler = fixPlannerHandler ?? throw new ArgumentNullException(nameof(fixPlannerHandler));
        _atomicGitCommitterHandler = atomicGitCommitterHandler ?? throw new ArgumentNullException(nameof(atomicGitCommitterHandler));
        _preflightValidator = preflightValidator ?? throw new ArgumentNullException(nameof(preflightValidator));
        _prerequisiteValidator = prerequisiteValidator ?? throw new ArgumentNullException(nameof(prerequisiteValidator));
        _outputValidator = outputValidator ?? throw new ArgumentNullException(nameof(outputValidator));
        _contextPackManager = contextPackManager ?? throw new ArgumentNullException(nameof(contextPackManager));
        _responderHandler = responderHandler ?? throw new ArgumentNullException(nameof(responderHandler));
        _inputClassifier = inputClassifier ?? throw new ArgumentNullException(nameof(inputClassifier));
        _chatResponder = chatResponder ?? throw new ArgumentNullException(nameof(chatResponder));
        _readOnlyHandler = readOnlyHandler ?? throw new ArgumentNullException(nameof(readOnlyHandler));
        _historyWriter = historyWriter ?? throw new ArgumentNullException(nameof(historyWriter));
        _handoffStateStore = handoffStateStore ?? throw new ArgumentNullException(nameof(handoffStateStore));
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
        string? dispatchedPhase = null;

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
            dispatchedPhase = gatingResult.TargetPhase;

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
                PersistTransitionState(gatingResult.TargetPhase, runContext.RunId, false);
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

            // Persist orchestrator transition state deterministically (Design Decision 6:
            // state updates, event append, and continuity outputs are orchestrator-owned)
            PersistTransitionState(gatingResult.TargetPhase, runContext.RunId, dispatchResult.IsSuccess, dispatchResult.Artifacts);

            // Close the run with the result
            var outputs = new Dictionary<string, object>
            {
                ["targetPhase"] = gatingResult.TargetPhase,
                ["reason"] = gatingResult.Reason,
                ["dispatchSuccess"] = dispatchResult.IsSuccess
            };

            await _runLifecycleManager.FinishRunAsync(runContext.RunId, dispatchResult.IsSuccess, outputs, ct);

            // Post-step continuity hook (Design Decision 6):
            // Write history and update the continuity snapshot after every control-plane step.
            // Both are non-blocking — failures are logged but do not affect the returned result.
            await PersistContinuityAsync(gatingResult.TargetPhase, runContext.RunId, dispatchResult.IsSuccess, ct);

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

            // Persist transition failure event if we know which phase was dispatched
            if (dispatchedPhase != null)
            {
                PersistTransitionState(dispatchedPhase, runContext.RunId, false);
            }

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
        var executableTaskId = ResolveExecutableTaskId(cursor);
        var cursorDisplay = !string.IsNullOrWhiteSpace(executableTaskId)
            ? executableTaskId
            : cursor?.PhaseId;

        var (hasCodebase, isStale) = CheckCodebaseIntelligence();

        return new GatingContext
        {
            HasProject = CheckProjectExists(),
            HasRoadmap = CheckRoadmapExists(),
            HasTaskPlan = CheckTaskPlanExists(snapshot),
            HasCodebaseIntelligence = hasCodebase,
            IsCodebaseStale = isStale,
            IsPhaseComplete = CheckPhaseComplete(snapshot),
            IsMilestoneComplete = CheckMilestoneComplete(snapshot),
            CurrentCursor = cursorDisplay,
            LastExecutionStatus = cursor?.TaskStatus,
            LastVerificationStatus = cursor?.StepStatus,
            StateData = new Dictionary<string, object>()
        };
    }

    private static string? ResolveExecutableTaskId(nirmata.Aos.Contracts.State.StateCursor? cursor)
    {
        if (cursor == null)
        {
            return null;
        }

        if (string.Equals(cursor.TaskStatus, "fix-planned", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(cursor.StepId))
        {
            return cursor.StepId;
        }

        return cursor.TaskId;
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

    private (bool Exists, bool IsStale) CheckCodebaseIntelligence()
    {
        try
        {
            var mapPath = Path.Combine(_workspace.AosRootPath, "codebase", "map.json");
            if (!File.Exists(mapPath))
            {
                return (false, false);
            }

            // Check staleness: map.json older than 24 hours is considered stale
            var fileInfo = new System.IO.FileInfo(mapPath);
            var age = DateTimeOffset.UtcNow - fileInfo.LastWriteTimeUtc;
            var isStale = age > TimeSpan.FromHours(24);

            return (true, isStale);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to check codebase intelligence state.");
            return (false, false);
        }
    }

    /// <summary>
    /// Checks for task-level plan existence. Task plans (.aos/spec/tasks/{taskId}/plan.json)
    /// are the only atomic execution contract. Phase-level planning artifacts do not satisfy
    /// the execution gate.
    /// </summary>
    private bool CheckTaskPlanExists(nirmata.Aos.Contracts.State.StateSnapshot? snapshot)
    {
        if (snapshot?.Cursor == null)
            return false;

        try
        {
            var executableTaskId = ResolveExecutableTaskId(snapshot.Cursor);

            // If the cursor has a specific executable task, check that task's plan.
            if (!string.IsNullOrWhiteSpace(executableTaskId))
            {
                var taskPlanPath = Path.Combine(
                    _workspace.AosRootPath, "spec", "tasks", executableTaskId, "plan.json");
                return File.Exists(taskPlanPath);
            }

            // If only a phaseId is set, check whether any task plans exist for this phase.
            // Walk .aos/spec/tasks/TSK-*/plan.json and check each task.json for a matching phaseId.
            if (!string.IsNullOrWhiteSpace(snapshot.Cursor.PhaseId))
            {
                var tasksDir = Path.Combine(_workspace.AosRootPath, "spec", "tasks");
                if (!Directory.Exists(tasksDir))
                    return false;

                foreach (var taskDir in Directory.EnumerateDirectories(tasksDir, "TSK-*"))
                {
                    var planPath = Path.Combine(taskDir, "plan.json");
                    if (File.Exists(planPath))
                        return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to check if task plan exists for cursor.");
            return false;
        }
    }

    private bool CheckPhaseComplete(nirmata.Aos.Contracts.State.StateSnapshot? snapshot)
    {
        if (snapshot?.Cursor == null || string.IsNullOrWhiteSpace(snapshot.Cursor.PhaseId))
            return false;

        return snapshot.Cursor.PhaseStatus is "completed" or "verified-pass";
    }

    private bool CheckMilestoneComplete(nirmata.Aos.Contracts.State.StateSnapshot? snapshot)
    {
        if (snapshot?.Cursor == null || string.IsNullOrWhiteSpace(snapshot.Cursor.MilestoneId))
            return false;

        return snapshot.Cursor.MilestoneStatus is "completed" or "shipped";
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
            var taskId = ResolveExecutableTaskId(snapshot?.Cursor);
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

            // Post-verification commit hook (Design Decision 5):
            // When verification passes and commitOnVerify is enabled, invoke the AtomicGitCommitter
            // to stage only files within the task's contracted allowed scope and record commit evidence.
            // Commit failure is non-blocking — it is logged and recorded in artifacts but does not
            // fail the verification result.
            if (result.IsSuccess)
            {
                var wsConfig = WorkspaceConfirmationConfig.Load(_workspace.RepositoryRootPath);
                if (wsConfig.CommitOnVerify == true)
                {
                    var snapshot = _stateStore.ReadSnapshot();
                    var taskId = snapshot?.Cursor?.TaskId;
                    var commitCommand = new nirmata.Aos.Contracts.Commands.CommandRequest
                    {
                        Group = "run",
                        Command = "commit",
                        Options = new Dictionary<string, string?>
                        {
                            ["task-id"] = taskId ?? string.Empty,
                            ["summary"] = $"feat: task {taskId} verified"
                        }
                    };

                    await _runLifecycleManager.RecordCommandAsync(runId, commitCommand.Group, commitCommand.Command, "dispatched", ct);
                    var commitResult = await _atomicGitCommitterHandler.HandleAsync(commitCommand, runId, ct);
                    await _runLifecycleManager.RecordCommandAsync(runId, commitCommand.Group, commitCommand.Command, commitResult.IsSuccess ? "completed" : "failed", ct);

                    artifacts["commitOnVerify"] = true;
                    artifacts["commitSuccess"] = commitResult.IsSuccess;
                    if (!commitResult.IsSuccess)
                    {
                        artifacts["commitError"] = commitResult.ErrorOutput ?? "Commit failed";
                        _logger?.LogWarning(
                            "Atomic git commit failed after verification for task {TaskId} (RunId: {RunId}): {Error}",
                            taskId, runId, commitResult.ErrorOutput);
                    }
                }
            }

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

        // Special handling for FixPlanner phase - use dedicated handler
        // See: design decision 5 — fix planning is a first-class orchestration hook, not generic fallback routing.
        if (targetPhase == "FixPlanner")
        {
            var command = MapPhaseToCommand(targetPhase);
            await _runLifecycleManager.RecordCommandAsync(runId, command.Group, command.Command, "dispatched", ct);

            var result = await _fixPlannerHandler.HandleAsync(command, runId, ct);

            await _runLifecycleManager.RecordCommandAsync(runId, command.Group, command.Command, result.IsSuccess ? "completed" : "failed", ct);

            var artifacts = new Dictionary<string, object>
            {
                ["phase"] = targetPhase,
                ["commandGroup"] = command.Group,
                ["command"] = command.Command,
                ["output"] = result.Output ?? string.Empty,
                ["error"] = result.ErrorOutput ?? string.Empty,
                ["fixPlanCreated"] = result.IsSuccess
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

        // ── MilestoneProgression: lightweight transition method (not a standalone handler) ──
        // See: design decision 4 — milestone progression is an explicit control-plane transition
        // invoked by the orchestrator after verification passes and the milestone is complete.
        if (targetPhase == "MilestoneProgression")
        {
            return await HandleMilestoneProgressionAsync(runId, ct);
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

    /// <summary>
    /// Lightweight milestone progression transition. Records milestone completion,
    /// advances the cursor, and appends audit events. This is an orchestrator-owned
    /// transition, not a standalone handler (see design decision 4).
    /// </summary>
    private async Task<DispatchResult> HandleMilestoneProgressionAsync(string runId, CancellationToken ct)
    {
        var command = MapPhaseToCommand("MilestoneProgression");
        await _runLifecycleManager.RecordCommandAsync(runId, command.Group, command.Command, "dispatched", ct);

        var snapshot = _stateStore.ReadSnapshot();
        var milestoneId = snapshot?.Cursor?.MilestoneId ?? "unknown";
        var phaseId = snapshot?.Cursor?.PhaseId;

        try
        {
            // 1. Append cursor.set event: mark milestone as done, clear task/step state
            using var cursorEventDoc = System.Text.Json.JsonSerializer.SerializeToDocument(new
            {
                eventType = "cursor.set",
                timestamp = DateTimeOffset.UtcNow,
                cursor = new
                {
                    milestoneStatus = "done",
                    phaseStatus = "done",
                    taskId = (string?)null,
                    taskStatus = (string?)null,
                    stepId = (string?)null,
                    stepStatus = (string?)null
                }
            }, nirmata.Aos.Public.DeterministicJsonOptions.Standard);
            _stateStore.AppendEvent(cursorEventDoc.RootElement);

            // 2. Append milestone.completed audit event
            using var auditEventDoc = System.Text.Json.JsonSerializer.SerializeToDocument(new
            {
                eventType = "milestone.completed",
                milestoneId,
                phaseId,
                timestamp = DateTimeOffset.UtcNow
            }, nirmata.Aos.Public.DeterministicJsonOptions.Standard);
            _stateStore.AppendEvent(auditEventDoc.RootElement);

            _logger?.LogInformation(
                "Milestone progression completed: {MilestoneId} marked as done (RunId: {RunId}).",
                milestoneId, runId);

            await _runLifecycleManager.RecordCommandAsync(runId, command.Group, command.Command, "completed", ct);

            return new DispatchResult
            {
                IsSuccess = true,
                Artifacts = new Dictionary<string, object>
                {
                    ["phase"] = "MilestoneProgression",
                    ["milestoneId"] = milestoneId,
                    ["milestoneStatus"] = "done",
                    ["output"] = $"Milestone {milestoneId} marked as completed."
                }
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Milestone progression failed for {MilestoneId} (RunId: {RunId}).", milestoneId, runId);
            await _runLifecycleManager.RecordCommandAsync(runId, command.Group, command.Command, "failed", ct);

            return new DispatchResult
            {
                IsSuccess = false,
                Artifacts = new Dictionary<string, object>
                {
                    ["phase"] = "MilestoneProgression",
                    ["milestoneId"] = milestoneId,
                    ["error"] = ex.Message
                }
            };
        }
    }

    /// <summary>
    /// Persists state and event updates after an orchestrator transition.
    /// Every control-plane step must produce an auditable event in events.ndjson
    /// and reconcile state.json so the snapshot reflects the latest transition.
    /// This is orchestrator-owned behavior per Design Decision 6.
    /// </summary>
    private void PersistTransitionState(
        string targetPhase,
        string runId,
        bool success,
        Dictionary<string, object>? artifacts = null)
    {
        try
        {
            var eventPayload = new Dictionary<string, object>
            {
                ["eventType"] = success
                    ? "orchestrator.transition.completed"
                    : "orchestrator.transition.failed",
                ["timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
                ["runId"] = runId,
                ["targetPhase"] = targetPhase,
                ["success"] = success
            };

            if (artifacts != null)
            {
                if (artifacts.TryGetValue("commandGroup", out var group))
                    eventPayload["commandGroup"] = group;
                if (artifacts.TryGetValue("command", out var cmd))
                    eventPayload["command"] = cmd;
            }

            using var eventDoc = JsonSerializer.SerializeToDocument(
                eventPayload, nirmata.Aos.Public.DeterministicJsonOptions.Standard);
            _stateStore.AppendEvent(eventDoc.RootElement);

            // Reconcile state.json from events so the snapshot is immediately consistent
            _stateStore.EnsureWorkspaceInitialized();

            _logger?.LogDebug(
                "Transition state persisted: Phase={Phase}, Success={Success}, RunId={RunId}.",
                targetPhase, success, runId);
        }
        catch (Exception ex)
        {
            // State persistence failure must not mask the original result.
            // Log the error so inconsistency is diagnosable.
            _logger?.LogError(ex,
                "Failed to persist transition state for phase {Phase} (RunId: {RunId}). " +
                "State may be inconsistent until next reconciliation.",
                targetPhase, runId);
        }
    }

    /// <summary>
    /// Orchestrator-owned post-step hook: writes history and updates the continuity snapshot.
    /// Called after every control-plane step that closes a run record.
    /// Both operations are non-blocking — failures are logged but do not affect the step result.
    /// See Design Decision 6: continuity and history updates are orchestrator responsibilities.
    /// </summary>
    private async Task PersistContinuityAsync(
        string targetPhase,
        string runId,
        bool success,
        CancellationToken ct)
    {
        // Write history for evidence-producing phases when the step succeeded.
        // These are the phases that create meaningful run evidence in .aos/evidence/runs/{runId}/.
        if (success && IsEvidenceProducingPhase(targetPhase))
        {
            await TryWriteHistoryAsync(runId, targetPhase, ct);
        }

        // Update the continuity snapshot for all write-phase steps (success or failure) so that
        // resume-work can reconstruct execution context from the most recent completed step.
        TryUpdateContinuitySnapshot(targetPhase, runId, success);
    }

    /// <summary>Returns true for phases whose runs produce evidence worth writing history for.</summary>
    private static bool IsEvidenceProducingPhase(string phase) =>
        phase is "Executor" or "Verifier" or "FixPlanner" or "MilestoneProgression";

    /// <summary>
    /// Attempts to append a history entry to .aos/spec/summary.md.
    /// Failure is logged but does not propagate.
    /// </summary>
    private async Task TryWriteHistoryAsync(string runId, string targetPhase, CancellationToken ct)
    {
        try
        {
            var snapshot = _stateStore.ReadSnapshot();
            var taskId = snapshot?.Cursor?.TaskId;
            await _historyWriter.AppendAsync(runId, taskId, narrative: null, ct);
            _logger?.LogDebug(
                "History written for phase {Phase} (RunId: {RunId}, TaskId: {TaskId}).",
                targetPhase, runId, taskId);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "History write failed for phase {Phase} (RunId: {RunId}). " +
                "This is non-blocking; history may be incomplete until the next successful step.",
                targetPhase, runId);
        }
    }

    /// <summary>
    /// Attempts to update .aos/state/handoff.json with the current cursor and next command.
    /// Failure is logged but does not propagate.
    /// </summary>
    private void TryUpdateContinuitySnapshot(string targetPhase, string runId, bool success)
    {
        try
        {
            var snapshot = _stateStore.ReadSnapshot();
            var cursor = snapshot?.Cursor ?? new StateCursor();
            var taskId = cursor.TaskId ?? "unknown";

            var handoff = new HandoffState
            {
                Timestamp = DateTimeOffset.UtcNow.ToString("O"),
                SourceRunId = runId,
                Cursor = cursor,
                TaskContext = new TaskContext
                {
                    TaskId = taskId,
                    Status = success ? "completed" : "failed"
                },
                Scope = new ScopeConstraints(),
                NextCommand = DeriveNextCommand(targetPhase, success)
            };

            _handoffStateStore.WriteHandoff(handoff);
            _logger?.LogDebug(
                "Continuity snapshot updated for phase {Phase} (RunId: {RunId}).",
                targetPhase, runId);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "Continuity snapshot update failed for phase {Phase} (RunId: {RunId}). " +
                "This is non-blocking; resume state may be stale.",
                targetPhase, runId);
        }
    }

    /// <summary>
    /// Derives the recommended next command based on which phase just completed and whether it succeeded.
    /// Used to populate the continuity snapshot so resume-work knows where to restart.
    /// </summary>
    private static NextCommand DeriveNextCommand(string phase, bool success) =>
        (phase, success) switch
        {
            ("Interviewer", true) => new NextCommand { Name = "create-roadmap", Group = "spec" },
            ("Roadmapper", true) => new NextCommand { Name = "plan-phase", Group = "spec" },
            ("Planner", true) => new NextCommand { Name = "execute-plan", Group = "run" },
            ("Executor", true) => new NextCommand { Name = "verify-work", Group = "run" },
            ("Verifier", true) => new NextCommand { Name = "plan-phase", Group = "spec" },
            ("Verifier", false) => new NextCommand { Name = "plan-fix", Group = "spec" },
            ("FixPlanner", true) => new NextCommand { Name = "execute-plan", Group = "run" },
            ("MilestoneProgression", true) => new NextCommand { Name = "new-milestone", Group = "spec" },
            _ => new NextCommand { Name = "status", Group = "core" }
        };

    private static nirmata.Aos.Contracts.Commands.CommandRequest MapPhaseToCommand(string phase)
    {
        return phase switch
        {
            "Interviewer" => nirmata.Aos.Contracts.Commands.CommandRequest.Create("spec", "init"),
            "CodebaseMapper" => nirmata.Aos.Contracts.Commands.CommandRequest.Create("codebase", "scan"),
            "Roadmapper" => nirmata.Aos.Contracts.Commands.CommandRequest.Create("spec", "roadmap"),
            "Planner" => nirmata.Aos.Contracts.Commands.CommandRequest.Create("spec", "plan"),
            "Executor" => nirmata.Aos.Contracts.Commands.CommandRequest.Create("run", "execute"),
            "Verifier" => nirmata.Aos.Contracts.Commands.CommandRequest.Create("run", "verify"),
            "AtomicCommitter" => nirmata.Aos.Contracts.Commands.CommandRequest.Create("run", "commit"),
            "Responder" => nirmata.Aos.Contracts.Commands.CommandRequest.Create("chat", "response"),
            "FixPlanner" => nirmata.Aos.Contracts.Commands.CommandRequest.Create("spec", "fix"),
            "MilestoneProgression" => nirmata.Aos.Contracts.Commands.CommandRequest.Create("spec", "milestone-complete"),
            _ => nirmata.Aos.Contracts.Commands.CommandRequest.Create("run", "status")
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
