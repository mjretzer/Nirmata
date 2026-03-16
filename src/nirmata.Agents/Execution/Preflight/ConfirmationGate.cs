using nirmata.Agents.Execution.ControlPlane;
using nirmata.Agents.Execution.ControlPlane.Streaming;

namespace nirmata.Agents.Execution.Preflight;

/// <summary>
/// Represents a pending confirmation request for ambiguous write operations.
/// </summary>
public sealed class ConfirmationRequest
{
    /// <summary>
    /// Unique identifier for this confirmation request.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The original user input that triggered this request.
    /// </summary>
    public required string OriginalInput { get; init; }

    /// <summary>
    /// The classified intent that requires confirmation.
    /// </summary>
    public required IntentClassifiedPayload ClassifiedIntent { get; init; }

    /// <summary>
    /// Human-readable description of the proposed action.
    /// </summary>
    public required string ActionDescription { get; init; }

    /// <summary>
    /// The confidence score that fell below the threshold.
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// The threshold that was not met.
    /// </summary>
    public double Threshold { get; init; } = 0.9;

    /// <summary>
    /// When the confirmation was requested.
    /// </summary>
    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optional timeout for the confirmation (null means no timeout).
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Additional context for the confirmation.
    /// </summary>
    public Dictionary<string, object>? Context { get; init; }
}

/// <summary>
/// Represents a user's response to a confirmation request.
/// </summary>
public sealed class ConfirmationResponse
{
    /// <summary>
    /// The ID of the confirmation request being responded to.
    /// </summary>
    public required string RequestId { get; init; }

    /// <summary>
    /// Whether the user confirmed the action.
    /// </summary>
    public bool Confirmed { get; init; }

    /// <summary>
    /// Optional user message accompanying the response.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// When the response was received.
    /// </summary>
    public DateTimeOffset RespondedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Result of evaluating a confirmation gate.
/// </summary>
public sealed class ConfirmationGateResult
{
    /// <summary>
    /// Whether the action can proceed without confirmation.
    /// </summary>
    public bool CanProceed { get; init; }

    /// <summary>
    /// Whether confirmation is required before proceeding.
    /// </summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>
    /// The confirmation request if one was generated.
    /// </summary>
    public ConfirmationRequest? Request { get; init; }

    /// <summary>
    /// The reason for the gate decision.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// The confidence score that was evaluated.
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Creates a result indicating the action can proceed without confirmation.
    /// </summary>
    public static ConfirmationGateResult Allow(string reason, double confidence = 1.0)
    {
        return new ConfirmationGateResult
        {
            CanProceed = true,
            RequiresConfirmation = false,
            Reason = reason,
            Confidence = confidence
        };
    }

    /// <summary>
    /// Creates a result indicating confirmation is required.
    /// </summary>
    public static ConfirmationGateResult RequireConfirmation(ConfirmationRequest request, double confidence, string? reason = null)
    {
        return new ConfirmationGateResult
        {
            CanProceed = false,
            RequiresConfirmation = true,
            Request = request,
            Reason = reason ?? $"Confidence ({confidence:F2}) below threshold ({request.Threshold:F2})",
            Confidence = confidence
        };
    }

    /// <summary>
    /// Creates a result indicating the action is blocked (not a workflow command).
    /// </summary>
    public static ConfirmationGateResult Block(string reason, double confidence = 0.0)
    {
        return new ConfirmationGateResult
        {
            CanProceed = false,
            RequiresConfirmation = false,
            Reason = reason,
            Confidence = confidence
        };
    }
}

/// <summary>
/// Configuration for the confirmation gate.
/// </summary>
public sealed class ConfirmationGateOptions
{
    /// <summary>
    /// The confidence threshold for write operations. Below this requires confirmation.
    /// </summary>
    public double ConfirmationThreshold { get; init; } = 0.9;

    /// <summary>
    /// Threshold for destructive operations (high bar due to irreversible nature).
    /// Default: 0.95
    /// </summary>
    public double DestructiveThreshold { get; init; } = 0.95;

    /// <summary>
    /// Threshold for write operations (file modifications, etc.).
    /// Default: 0.8
    /// </summary>
    public double WriteThreshold { get; init; } = 0.8;

    /// <summary>
    /// Threshold for ambiguous or unclear operations.
    /// Default: 0.7
    /// </summary>
    public double AmbiguousThreshold { get; init; } = 0.7;

    /// <summary>
    /// Whether to require confirmation for all write operations regardless of confidence.
    /// </summary>
    public bool AlwaysConfirmWrites { get; init; } = false;

    /// <summary>
    /// Timeout for confirmation requests.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Commands that never require confirmation (explicit overrides).
    /// </summary>
    public HashSet<string> NoConfirmationCommands { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "status", "help", "?"
    };

    /// <summary>
    /// Gets the appropriate threshold for a given risk level.
    /// </summary>
    public double GetThresholdForRiskLevel(RiskLevel riskLevel) => riskLevel switch
    {
        RiskLevel.WriteDestructiveGit => DestructiveThreshold,
        RiskLevel.WorkspaceDestructive => DestructiveThreshold,
        RiskLevel.WriteDestructive => WriteThreshold,
        RiskLevel.WriteSafe => AmbiguousThreshold,
        RiskLevel.Read => 0.0, // Read operations don't require confirmation
        _ => ConfirmationThreshold
    };
}

/// <summary>
/// Gate that determines whether a workflow operation requires user confirmation.
/// Used for ambiguous classifications or low-confidence write operations.
/// </summary>
public interface IConfirmationGate
{
    /// <summary>
    /// Evaluates whether an intent requires confirmation before execution.
    /// </summary>
    /// <param name="classification">The intent classification result to evaluate.</param>
    /// <param name="options">Configuration options for the gate.</param>
    /// <returns>Result indicating whether confirmation is required.</returns>
    ConfirmationGateResult Evaluate(IntentClassificationResult classification, ConfirmationGateOptions? options = null);

    /// <summary>
    /// Evaluates whether a suggested command requires confirmation before execution.
    /// </summary>
    /// <param name="classification">The intent classification result containing the suggestion.</param>
    /// <param name="proposal">The command proposal to evaluate.</param>
    /// <param name="options">Configuration options for the gate.</param>
    /// <returns>Result indicating whether confirmation is required.</returns>
    ConfirmationGateResult EvaluateSuggestion(IntentClassificationResult classification, CommandSuggestion.CommandProposal proposal, ConfirmationGateOptions? options = null);

    /// <summary>
    /// Processes a confirmation response and determines if the action can proceed.
    /// </summary>
    /// <param name="response">The user's confirmation response.</param>
    /// <returns>True if the action should proceed, false otherwise.</returns>
    bool ProcessResponse(ConfirmationResponse response);

    /// <summary>
    /// Evaluates workspace prerequisites and returns missing items with conversational recovery options.
    /// </summary>
    /// <param name="workspaceRoot">The workspace root directory path.</param>
    /// <returns>Prerequisite check result indicating satisfaction or missing prerequisites.</returns>
    PrerequisiteCheckResult EvaluatePrerequisites(string workspaceRoot);

    /// <summary>
    /// Evaluates whether an intent requires confirmation using destructiveness analysis.
    /// Integrates with DestructivenessAnalyzer to apply risk-level based confirmation requirements.
    /// </summary>
    /// <param name="classification">The intent classification result to evaluate.</param>
    /// <param name="phase">The target workflow phase.</param>
    /// <param name="gatingContext">The gating context for workspace state.</param>
    /// <param name="options">Configuration options for the gate.</param>
    /// <returns>Result indicating whether confirmation is required based on destructiveness analysis.</returns>
    ConfirmationGateResult EvaluateWithDestructiveness(
        IntentClassificationResult classification,
        string phase,
        GatingContext gatingContext,
        ConfirmationGateOptions? options = null);

    /// <summary>
    /// Detects git operations from command arguments and returns the appropriate risk level.
    /// </summary>
    /// <param name="arguments">Command arguments to analyze.</param>
    /// <returns>Risk level for git operations, or null if no git operations detected.</returns>
    RiskLevel? DetectGitOperationRisk(IReadOnlyList<string> arguments);

    /// <summary>
    /// Detects file system scope changes that would elevate the risk level to destructive.
    /// Considers broad file patterns, critical paths, and high-volume operations.
    /// </summary>
    /// <param name="classification">The intent classification result.</param>
    /// <param name="phase">The target workflow phase.</param>
    /// <returns>Elevated risk level if scope is destructive, otherwise null.</returns>
    RiskLevel? DetectFileSystemScopeRisk(IntentClassificationResult classification, string phase);
}

/// <summary>
/// Default implementation of the confirmation gate.
/// </summary>
public sealed class ConfirmationGate : IConfirmationGate
{
    private readonly Dictionary<string, ConfirmationRequest> _pendingConfirmations = new();
    private readonly ConfirmationEventPublisher? _eventPublisher;
    private readonly IAmbiguityAnalyzer _ambiguityAnalyzer;
    private readonly IDestructivenessAnalyzer _destructivenessAnalyzer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfirmationGate"/> class.
    /// </summary>
    public ConfirmationGate()
    {
        _ambiguityAnalyzer = new AmbiguityAnalyzer();
        _destructivenessAnalyzer = new DestructivenessAnalyzer();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfirmationGate"/> class with destructiveness analyzer.
    /// </summary>
    /// <param name="destructivenessAnalyzer">The destructiveness analyzer for risk assessment.</param>
    public ConfirmationGate(IDestructivenessAnalyzer destructivenessAnalyzer)
    {
        _ambiguityAnalyzer = new AmbiguityAnalyzer();
        _destructivenessAnalyzer = destructivenessAnalyzer;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfirmationGate"/> class with event publishing.
    /// </summary>
    /// <param name="eventPublisher">The event publisher for streaming confirmation events.</param>
    public ConfirmationGate(ConfirmationEventPublisher eventPublisher)
    {
        _eventPublisher = eventPublisher;
        _ambiguityAnalyzer = new AmbiguityAnalyzer();
        _destructivenessAnalyzer = new DestructivenessAnalyzer();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfirmationGate"/> class with event publishing and destructiveness analyzer.
    /// </summary>
    /// <param name="eventPublisher">The event publisher for streaming confirmation events.</param>
    /// <param name="destructivenessAnalyzer">The destructiveness analyzer for risk assessment.</param>
    public ConfirmationGate(ConfirmationEventPublisher eventPublisher, IDestructivenessAnalyzer destructivenessAnalyzer)
    {
        _eventPublisher = eventPublisher;
        _ambiguityAnalyzer = new AmbiguityAnalyzer();
        _destructivenessAnalyzer = destructivenessAnalyzer;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfirmationGate"/> class with event publishing and analyzers.
    /// </summary>
    /// <param name="eventPublisher">The event publisher for streaming confirmation events.</param>
    /// <param name="ambiguityAnalyzer">The ambiguity analyzer for detecting fuzzy intent.</param>
    /// <param name="destructivenessAnalyzer">The destructiveness analyzer for risk assessment.</param>
    public ConfirmationGate(ConfirmationEventPublisher eventPublisher, IAmbiguityAnalyzer ambiguityAnalyzer, IDestructivenessAnalyzer destructivenessAnalyzer)
    {
        _eventPublisher = eventPublisher;
        _ambiguityAnalyzer = ambiguityAnalyzer;
        _destructivenessAnalyzer = destructivenessAnalyzer;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfirmationGate"/> class with ambiguity analyzer only.
    /// </summary>
    /// <param name="ambiguityAnalyzer">The ambiguity analyzer for detecting fuzzy intent.</param>
    public ConfirmationGate(IAmbiguityAnalyzer ambiguityAnalyzer)
    {
        _ambiguityAnalyzer = ambiguityAnalyzer;
        _destructivenessAnalyzer = new DestructivenessAnalyzer();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfirmationGate"/> class with ambiguity analyzer and destructiveness analyzer.
    /// </summary>
    /// <param name="ambiguityAnalyzer">The ambiguity analyzer for detecting fuzzy intent.</param>
    /// <param name="destructivenessAnalyzer">The destructiveness analyzer for risk assessment.</param>
    public ConfirmationGate(IAmbiguityAnalyzer ambiguityAnalyzer, IDestructivenessAnalyzer destructivenessAnalyzer)
    {
        _ambiguityAnalyzer = ambiguityAnalyzer;
        _destructivenessAnalyzer = destructivenessAnalyzer;
    }

    /// <summary>
    /// Evaluates whether an intent requires confirmation using workspace-level configuration.
    /// </summary>
    /// <param name="classification">The intent classification result to evaluate.</param>
    /// <param name="workspaceRoot">The workspace root for loading configuration.</param>
    /// <param name="riskLevel">The risk level of the operation.</param>
    /// <returns>Result indicating whether confirmation is required.</returns>
    public ConfirmationGateResult EvaluateWithWorkspaceConfig(
        IntentClassificationResult classification,
        string workspaceRoot,
        RiskLevel riskLevel = RiskLevel.WriteSafe)
    {
        var baseOptions = new ConfirmationGateOptions();
        var workspaceConfig = WorkspaceConfirmationConfig.Load(workspaceRoot);
        var options = workspaceConfig.ApplyTo(baseOptions);

        // Apply operation-specific threshold if configured
        var operationType = classification.ParsedCommand?.CommandName;
        if (operationType != null)
        {
            var operationThreshold = workspaceConfig.GetOperationThreshold(operationType);
            if (operationThreshold.HasValue)
            {
                options = new ConfirmationGateOptions
                {
                    ConfirmationThreshold = operationThreshold.Value,
                    DestructiveThreshold = options.DestructiveThreshold,
                    WriteThreshold = options.WriteThreshold,
                    AmbiguousThreshold = options.AmbiguousThreshold,
                    AlwaysConfirmWrites = options.AlwaysConfirmWrites,
                    Timeout = options.Timeout,
                    NoConfirmationCommands = options.NoConfirmationCommands
                };
            }
        }

        // Use risk-level based threshold
        var threshold = options.GetThresholdForRiskLevel(riskLevel);

        return EvaluateWithThreshold(classification, options, threshold);
    }

    private ConfirmationGateResult EvaluateWithThreshold(
        IntentClassificationResult classification,
        ConfirmationGateOptions options,
        double threshold)
    {
        var intent = classification.Intent;
        var input = classification.ParsedCommand?.RawInput ?? string.Empty;

        // Analyze for ambiguity signals
        var ambiguitySignals = _ambiguityAnalyzer.Analyze(input, classification, threshold);

        // Check if this is a suggested command - always require confirmation for suggestions
        if (classification.HasSuggestion())
        {
            return EvaluateSuggestion(classification, classification.SuggestedCommand!, options);
        }

        // Read-only operations and chat never require confirmation
        if (intent.SideEffect == SideEffect.None || intent.SideEffect == SideEffect.ReadOnly)
        {
            return ConfirmationGateResult.Allow("Read-only or chat intent - no confirmation needed", intent.Confidence);
        }

        // Check for explicit no-confirmation commands
        if (classification.ParsedCommand?.CommandName is { } cmdName &&
            options.NoConfirmationCommands.Contains(cmdName))
        {
            return ConfirmationGateResult.Allow($"Command '{cmdName}' in no-confirmation list", intent.Confidence);
        }

        // Check for ambiguity - if ambiguous, require confirmation with ambiguous threshold
        if (ambiguitySignals.IsAmbiguous && intent.SideEffect == SideEffect.Write)
        {
            var ambiguousThreshold = options.AmbiguousThreshold;
            var request = CreateConfirmationRequest(classification, options, ambiguousThreshold, ambiguitySignals);
            _pendingConfirmations[request.Id] = request;
            return ConfirmationGateResult.RequireConfirmation(
                request,
                intent.Confidence,
                $"Ambiguous intent detected: {ambiguitySignals.Reasoning}");
        }

        // If always confirm writes is enabled
        if (options.AlwaysConfirmWrites)
        {
            var request = CreateConfirmationRequest(classification, options, threshold);
            _pendingConfirmations[request.Id] = request;
            return ConfirmationGateResult.RequireConfirmation(request, intent.Confidence);
        }

        // Check confidence against risk-level threshold
        if (intent.Confidence >= threshold)
        {
            return ConfirmationGateResult.Allow(
                $"Confidence ({intent.Confidence:F2}) meets threshold ({threshold:F2})",
                intent.Confidence);
        }

        // Confidence below threshold - require confirmation
        var confirmationRequest = CreateConfirmationRequest(classification, options, threshold);
        _pendingConfirmations[confirmationRequest.Id] = confirmationRequest;

        return ConfirmationGateResult.RequireConfirmation(confirmationRequest, intent.Confidence);
    }

    private ConfirmationRequest CreateConfirmationRequest(
        IntentClassificationResult classification,
        ConfirmationGateOptions options,
        double threshold,
        AmbiguitySignals? ambiguitySignals = null)
    {
        var intent = classification.Intent;
        var context = new Dictionary<string, object>();

        if (ambiguitySignals != null && ambiguitySignals.IsAmbiguous)
        {
            context["ambiguityLevel"] = ambiguitySignals.Level.ToString();
            context["ambiguityReasoning"] = ambiguitySignals.Reasoning ?? "Ambiguous intent detected";
            context["hasLowConfidence"] = ambiguitySignals.HasLowConfidence;
            context["hasVagueVerbs"] = ambiguitySignals.HasVagueVerbs;
            context["detectedVagueVerbs"] = ambiguitySignals.DetectedVagueVerbs;
            context["hasMissingContext"] = ambiguitySignals.HasMissingContext;
        }

        return new ConfirmationRequest
        {
            OriginalInput = classification.ParsedCommand?.RawInput ?? string.Empty,
            ClassifiedIntent = new IntentClassifiedPayload
            {
                Category = intent.Kind.ToString(),
                Confidence = intent.Confidence,
                Reasoning = intent.Reasoning,
                UserInput = classification.ParsedCommand?.RawInput
            },
            ActionDescription = BuildActionDescription(intent, classification.ParsedCommand),
            Confidence = intent.Confidence,
            Threshold = threshold,
            Timeout = options.Timeout,
            Context = context.Count > 0 ? context : null
        };
    }

    /// <inheritdoc />
    public ConfirmationGateResult Evaluate(IntentClassificationResult classification, ConfirmationGateOptions? options = null)
    {
        options ??= new ConfirmationGateOptions();

        var intent = classification.Intent;

        // Check if this is a suggested command - always require confirmation for suggestions
        if (classification.HasSuggestion())
        {
            return EvaluateSuggestion(classification, classification.SuggestedCommand!, options);
        }

        // Read-only operations and chat never require confirmation
        if (intent.SideEffect == SideEffect.None || intent.SideEffect == SideEffect.ReadOnly)
        {
            return ConfirmationGateResult.Allow("Read-only or chat intent - no confirmation needed", intent.Confidence);
        }

        // Check for explicit no-confirmation commands
        if (classification.ParsedCommand?.CommandName is { } cmdName &&
            options.NoConfirmationCommands.Contains(cmdName))
        {
            return ConfirmationGateResult.Allow($"Command '{cmdName}' in no-confirmation list", intent.Confidence);
        }

        // If always confirm writes is enabled
        if (options.AlwaysConfirmWrites)
        {
            var request = new ConfirmationRequest
            {
                OriginalInput = classification.ParsedCommand?.RawInput ?? string.Empty,
                ClassifiedIntent = new IntentClassifiedPayload
                {
                    Category = intent.Kind.ToString(),
                    Confidence = intent.Confidence,
                    Reasoning = intent.Reasoning,
                    UserInput = classification.ParsedCommand?.RawInput
                },
                ActionDescription = BuildActionDescription(intent, classification.ParsedCommand),
                Confidence = intent.Confidence,
                Threshold = options.ConfirmationThreshold,
                Timeout = options.Timeout
            };

            _pendingConfirmations[request.Id] = request;

            return ConfirmationGateResult.RequireConfirmation(request, intent.Confidence);
        }

        // Check confidence against threshold
        if (intent.Confidence >= options.ConfirmationThreshold)
        {
            return ConfirmationGateResult.Allow(
                $"Confidence ({intent.Confidence:F2}) meets threshold ({options.ConfirmationThreshold:F2})",
                intent.Confidence);
        }

        // Confidence below threshold - require confirmation
        var confirmationRequest = new ConfirmationRequest
        {
            OriginalInput = classification.ParsedCommand?.RawInput ?? string.Empty,
            ClassifiedIntent = new IntentClassifiedPayload
            {
                Category = intent.Kind.ToString(),
                Confidence = intent.Confidence,
                Reasoning = intent.Reasoning,
                UserInput = classification.ParsedCommand?.RawInput
            },
            ActionDescription = BuildActionDescription(intent, classification.ParsedCommand),
            Confidence = intent.Confidence,
            Threshold = options.ConfirmationThreshold,
            Timeout = options.Timeout
        };

        _pendingConfirmations[confirmationRequest.Id] = confirmationRequest;

        return ConfirmationGateResult.RequireConfirmation(confirmationRequest, intent.Confidence);
    }

    /// <inheritdoc />
    public ConfirmationGateResult EvaluateSuggestion(IntentClassificationResult classification, CommandSuggestion.CommandProposal proposal, ConfirmationGateOptions? options = null)
    {
        options ??= new ConfirmationGateOptions();

        // Suggestions always require confirmation before execution
        var request = new ConfirmationRequest
        {
            OriginalInput = classification.Intent.Reasoning ?? string.Empty,
            ClassifiedIntent = new IntentClassifiedPayload
            {
                Category = "SuggestedCommand",
                Confidence = proposal.Confidence,
                Reasoning = proposal.Reasoning ?? $"Suggested command: {proposal.FormattedCommand}",
                UserInput = proposal.FormattedCommand
            },
            ActionDescription = BuildSuggestionActionDescription(proposal),
            Confidence = proposal.Confidence,
            Threshold = options.ConfirmationThreshold,
            Timeout = options.Timeout,
            Context = new Dictionary<string, object>
            {
                ["suggestionSource"] = classification.SuggestionSource ?? "llm",
                ["suggestedCommand"] = proposal.CommandName,
                ["suggestedArguments"] = proposal.Arguments,
                ["formattedCommand"] = proposal.FormattedCommand ?? $"/{proposal.CommandName}"
            }
        };

        _pendingConfirmations[request.Id] = request;

        return ConfirmationGateResult.RequireConfirmation(request, proposal.Confidence);
    }

    /// <inheritdoc />
    public bool ProcessResponse(ConfirmationResponse response)
    {
        if (!_pendingConfirmations.TryGetValue(response.RequestId, out var request))
        {
            _eventPublisher?.PublishResponded(response.RequestId, false, "Unknown confirmation ID");
            return false;
        }

        _pendingConfirmations.Remove(response.RequestId);

        // Check for timeout
        if (request.Timeout.HasValue &&
            DateTimeOffset.UtcNow - request.RequestedAt > request.Timeout.Value)
        {
            _eventPublisher?.PublishTimeout(response.RequestId, request.RequestedAt, request.Timeout.Value);
            return false;
        }

        _eventPublisher?.PublishResponded(response.RequestId, response.Confirmed, response.Message);
        return response.Confirmed;
    }

    /// <inheritdoc />
    public PrerequisiteCheckResult EvaluatePrerequisites(string workspaceRoot)
    {
        // Check for .aos/spec directory
        var specPath = Path.Combine(workspaceRoot, ".aos", "spec");
        if (!Directory.Exists(specPath))
        {
            return PrerequisiteCheckResult.MissingPrerequisite(new MissingPrerequisite
            {
                PrerequisiteType = "SpecDirectory",
                Description = "Project specification directory is missing",
                ExpectedPath = specPath,
                RecoveryAction = "Interviewer",
                ConversationalPrompt = "I need a project specification before proceeding. Would you like me to start the project interviewer to create one?"
            });
        }

        // Check for .aos/state directory
        var statePath = Path.Combine(workspaceRoot, ".aos", "state");
        if (!Directory.Exists(statePath))
        {
            return PrerequisiteCheckResult.MissingPrerequisite(new MissingPrerequisite
            {
                PrerequisiteType = "StateDirectory",
                Description = "Workspace state directory is missing",
                ExpectedPath = statePath,
                RecoveryAction = "init",
                ConversationalPrompt = "The workspace state directory is missing. Should I initialize the workspace state?"
            });
        }

        // Check for .aos/context directory
        var contextPath = Path.Combine(workspaceRoot, ".aos", "context");
        if (!Directory.Exists(contextPath))
        {
            return PrerequisiteCheckResult.MissingPrerequisite(new MissingPrerequisite
            {
                PrerequisiteType = "ContextDirectory",
                Description = "Workspace context directory is missing",
                ExpectedPath = contextPath,
                RecoveryAction = "init",
                ConversationalPrompt = "The workspace context directory is missing. Should I initialize the workspace context?"
            });
        }

        return PrerequisiteCheckResult.Satisfied();
    }

    /// <summary>
    /// Evaluates whether an intent requires confirmation using destructiveness analysis.
    /// This method integrates with the DestructivenessAnalyzer to apply risk-level based
    /// confirmation requirements (Task 3.1, 3.2).
    /// </summary>
    /// <param name="classification">The intent classification result to evaluate.</param>
    /// <param name="phase">The target workflow phase.</param>
    /// <param name="gatingContext">The gating context for workspace state.</param>
    /// <param name="options">Configuration options for the gate.</param>
    /// <returns>Result indicating whether confirmation is required based on destructiveness analysis.</returns>
    public ConfirmationGateResult EvaluateWithDestructiveness(
        IntentClassificationResult classification,
        string phase,
        GatingContext gatingContext,
        ConfirmationGateOptions? options = null)
    {
        options ??= new ConfirmationGateOptions();
        var intent = classification.Intent;

        // Read-only operations and chat never require confirmation
        if (intent.SideEffect == SideEffect.None || intent.SideEffect == SideEffect.ReadOnly)
        {
            return ConfirmationGateResult.Allow("Read-only or chat intent - no confirmation needed", intent.Confidence);
        }

        // Analyze base risk level from phase
        var baseRiskLevel = _destructivenessAnalyzer.AnalyzeRisk(phase, gatingContext);

        // Check for git operations in command arguments (Task 3.3)
        if (classification.ParsedCommand?.Arguments is { Length: > 0 } args)
        {
            var gitRisk = DetectGitOperationRisk(args);
            if (gitRisk != null)
            {
                baseRiskLevel = gitRisk.Value;
            }
        }

        // Check for file system scope changes (Task 3.4)
        var scopeRisk = DetectFileSystemScopeRisk(classification, phase);
        if (scopeRisk.HasValue)
        {
            baseRiskLevel = scopeRisk.Value;
        }

        // Map destructiveness levels to confirmation requirements (Task 3.2)
        // WriteDestructive, WriteDestructiveGit, and WorkspaceDestructive always require confirmation
        if (baseRiskLevel == RiskLevel.WriteDestructive ||
            baseRiskLevel == RiskLevel.WriteDestructiveGit ||
            baseRiskLevel == RiskLevel.WorkspaceDestructive)
        {
            var destructiveThreshold = options.GetThresholdForRiskLevel(baseRiskLevel);
            var request = CreateConfirmationRequestWithRisk(
                classification,
                options,
                destructiveThreshold,
                baseRiskLevel,
                "Destructive operation detected - confirmation required");
            _pendingConfirmations[request.Id] = request;
            return ConfirmationGateResult.RequireConfirmation(
                request,
                intent.Confidence,
                $"Destructive operation ({baseRiskLevel}) requires confirmation");
        }

        // For WriteSafe operations, use standard threshold-based evaluation
        var threshold = options.GetThresholdForRiskLevel(baseRiskLevel);

        if (intent.Confidence >= threshold)
        {
            return ConfirmationGateResult.Allow(
                $"Confidence ({intent.Confidence:F2}) meets threshold ({threshold:F2}) for {baseRiskLevel}",
                intent.Confidence);
        }

        // Confidence below threshold - require confirmation
        var confirmationRequest = CreateConfirmationRequestWithRisk(
            classification,
            options,
            threshold,
            baseRiskLevel);
        _pendingConfirmations[confirmationRequest.Id] = confirmationRequest;

        return ConfirmationGateResult.RequireConfirmation(confirmationRequest, intent.Confidence);
    }

    /// <summary>
    /// Detects git operations from command arguments and returns the appropriate risk level.
    /// Supports git commit, push, merge, rebase, reset, cherry-pick, revert, and tag operations.
    /// (Task 3.3)
    /// </summary>
    /// <param name="arguments">Command arguments to analyze.</param>
    /// <returns>Risk level for git operations, or null if no git operations detected.</returns>
    public RiskLevel? DetectGitOperationRisk(IReadOnlyList<string> arguments)
    {
        // Join arguments to analyze as a single string for pattern matching
        var argsText = string.Join(" ", arguments).ToLowerInvariant();

        // Check for git operation indicators
        if (!argsText.Contains("git"))
        {
            return null;
        }

        // Extract git operation from arguments
        var gitOperations = new[] { "commit", "push", "merge", "rebase", "reset", "cherry-pick", "revert", "tag" };
        var detectedOperation = gitOperations.FirstOrDefault(op => argsText.Contains(op));

        if (detectedOperation != null)
        {
            return _destructivenessAnalyzer.AnalyzeGitOperationRisk(detectedOperation, arguments);
        }

        // Check for destructive git flags
        if (argsText.Contains("--force") || argsText.Contains("-f") ||
            argsText.Contains("--hard") || argsText.Contains("--delete"))
        {
            return RiskLevel.WriteDestructiveGit;
        }

        return null;
    }

    /// <summary>
    /// Detects file system scope changes that would elevate the risk level to destructive.
    /// Considers broad file patterns, critical paths, and high-volume operations.
    /// (Task 3.4)
    /// </summary>
    /// <param name="classification">The intent classification result.</param>
    /// <param name="phase">The target workflow phase.</param>
    /// <returns>Elevated risk level if scope is destructive, otherwise null.</returns>
    public RiskLevel? DetectFileSystemScopeRisk(IntentClassificationResult classification, string phase)
    {
        var input = classification.ParsedCommand?.RawInput ?? string.Empty;
        var args = classification.ParsedCommand?.Arguments ?? [];

        // Check for broad wildcard patterns that affect many files
        var argsText = string.Join(" ", args);
        var hasBroadWildcards = argsText.Contains("*") || argsText.Contains("?") ||
                               argsText.Contains("**") || argsText.Contains("...");

        // Check for recursive operations
        var hasRecursiveFlag = args.Any(a =>
            a.Equals("-r", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--recursive", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/s", StringComparison.OrdinalIgnoreCase));

        // Check for critical system paths
        var criticalPaths = new[] { ".aos", ".git", "node_modules", ".env", "/etc", "/usr", "C:\\Windows" };
        var affectsCriticalPaths = criticalPaths.Any(cp =>
            argsText.Contains(cp, StringComparison.OrdinalIgnoreCase));

        // Check for deletion operations
        var deleteIndicators = new[] { "delete", "remove", "rm", "del", "drop", "clean", "prune" };
        var hasDeleteOperation = deleteIndicators.Any(d =>
            input.Contains(d, StringComparison.OrdinalIgnoreCase) ||
            args.Any(a => a.Equals(d, StringComparison.OrdinalIgnoreCase)));

        // Elevate to WorkspaceDestructive for high-risk combinations
        if (hasDeleteOperation && (hasBroadWildcards || hasRecursiveFlag))
        {
            return RiskLevel.WorkspaceDestructive;
        }

        // Elevate to WriteDestructive for operations affecting critical paths
        if (affectsCriticalPaths && (hasDeleteOperation || phase == "Executor"))
        {
            return RiskLevel.WriteDestructive;
        }

        // Elevate for Executor phase with broad scope
        if (phase == "Executor" && hasBroadWildcards)
        {
            return RiskLevel.WriteDestructive;
        }

        return null;
    }

    private ConfirmationRequest CreateConfirmationRequestWithRisk(
        IntentClassificationResult classification,
        ConfirmationGateOptions options,
        double threshold,
        RiskLevel riskLevel,
        string? reason = null)
    {
        var intent = classification.Intent;
        var context = new Dictionary<string, object>
        {
            ["riskLevel"] = riskLevel.ToString(),
            ["detectedAt"] = DateTimeOffset.UtcNow.ToString("O")
        };

        if (!string.IsNullOrEmpty(reason))
        {
            context["reason"] = reason;
        }

        return new ConfirmationRequest
        {
            OriginalInput = classification.ParsedCommand?.RawInput ?? string.Empty,
            ClassifiedIntent = new IntentClassifiedPayload
            {
                Category = intent.Kind.ToString(),
                Confidence = intent.Confidence,
                Reasoning = intent.Reasoning,
                UserInput = classification.ParsedCommand?.RawInput
            },
            ActionDescription = BuildActionDescriptionWithRisk(intent, classification.ParsedCommand, riskLevel),
            Confidence = intent.Confidence,
            Threshold = threshold,
            Timeout = options.Timeout,
            Context = context
        };
    }

    private static string BuildActionDescriptionWithRisk(Intent intent, ParsedCommand? command, RiskLevel riskLevel)
    {
        var baseDescription = BuildActionDescription(intent, command);
        return $"{baseDescription} [Risk: {riskLevel}]";
    }

    /// <summary>
    /// Gets a pending confirmation by ID.
    /// </summary>
    public ConfirmationRequest? GetPendingConfirmation(string id)
    {
        _pendingConfirmations.TryGetValue(id, out var request);
        return request;
    }

    private static string BuildActionDescription(Intent intent, ParsedCommand? command)
    {
        if (command != null)
        {
            return $"Execute '{command.CommandName}' command ({intent.SideEffect} side effect)";
        }

        return $"Execute workflow action: {intent.Kind} ({intent.SideEffect} side effect)";
    }

    private static string BuildSuggestionActionDescription(CommandSuggestion.CommandProposal proposal)
    {
        return $"Execute suggested command '{proposal.FormattedCommand}' (confidence: {proposal.Confidence:F2})";
    }
}

/// <summary>
/// Represents a missing prerequisite with recovery options.
/// </summary>
public sealed class MissingPrerequisite
{
    /// <summary>
    /// The type of prerequisite that is missing.
    /// </summary>
    public required string PrerequisiteType { get; init; }

    /// <summary>
    /// Human-readable description of what's missing.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The workspace path that should exist.
    /// </summary>
    public required string ExpectedPath { get; init; }

    /// <summary>
    /// Suggested recovery action (command or phase to run).
    /// </summary>
    public string? RecoveryAction { get; init; }

    /// <summary>
    /// Human-readable message to present to the user.
    /// </summary>
    public string? ConversationalPrompt { get; init; }
}

/// <summary>
/// Result of a prerequisite check.
/// </summary>
public sealed class PrerequisiteCheckResult
{
    /// <summary>
    /// Whether all prerequisites are satisfied.
    /// </summary>
    public bool IsSatisfied { get; init; }

    /// <summary>
    /// The missing prerequisite if any.
    /// </summary>
    public MissingPrerequisite? Missing { get; init; }

    /// <summary>
    /// Creates a satisfied result.
    /// </summary>
    public static PrerequisiteCheckResult Satisfied() => new() { IsSatisfied = true };

    /// <summary>
    /// Creates a result with a missing prerequisite.
    /// </summary>
    public static PrerequisiteCheckResult MissingPrerequisite(MissingPrerequisite missing) =>
        new() { IsSatisfied = false, Missing = missing };
}
public class IntentClassifiedPayload
{
    /// <summary>
    /// The classified intent category (e.g., "Chat", "Write", "Plan", "Execute")
    /// </summary>
    public required string Category { get; set; }

    /// <summary>
    /// Confidence score between 0.0 and 1.0
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Reasoning or explanation for the classification decision
    /// </summary>
    public string? Reasoning { get; set; }

    /// <summary>
    /// The original user input that was classified
    /// </summary>
    public string? UserInput { get; set; }
}
