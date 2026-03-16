using nirmata.Agents.Execution.Preflight;
using Microsoft.Extensions.Options;

namespace nirmata.Agents.Execution.ControlPlane;

/// <summary>
/// Evaluates whether a gating result requires user confirmation based on risk level,
/// confidence thresholds, and destructiveness analysis.
/// </summary>
public interface IConfirmationGateEvaluator
{
    /// <summary>
    /// Evaluates the gating result and context to determine if confirmation is required.
    /// </summary>
    /// <param name="gatingResult">The gating result containing the target phase and proposed action.</param>
    /// <param name="context">The gating context containing workspace state.</param>
    /// <param name="classification">The intent classification result from the input classifier.</param>
    /// <returns>Evaluation result indicating whether confirmation is required.</returns>
    ConfirmationEvaluationResult Evaluate(
        GatingResult gatingResult,
        GatingContext context,
        IntentClassificationResult classification);

    /// <summary>
    /// Evaluates whether a confirmation response allows the action to proceed.
    /// </summary>
    /// <param name="confirmationId">The ID of the confirmation to evaluate.</param>
    /// <param name="response">The user's response to the confirmation request.</param>
    /// <returns>True if the action should proceed, false otherwise.</returns>
    bool EvaluateResponse(string confirmationId, ConfirmationResponse response);
}

/// <summary>
/// Result of a confirmation gate evaluation.
/// </summary>
public sealed class ConfirmationEvaluationResult
{
    /// <summary>
    /// Whether the action requires confirmation before proceeding.
    /// </summary>
    public required bool RequiresConfirmation { get; init; }

    /// <summary>
    /// Whether the action can proceed (either confirmed or not required).
    /// </summary>
    public required bool CanProceed { get; init; }

    /// <summary>
    /// The reason for the evaluation decision.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// The confidence score that was evaluated.
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// The threshold that was applied.
    /// </summary>
    public double Threshold { get; init; }

    /// <summary>
    /// The confirmation request if confirmation is required.
    /// </summary>
    public ConfirmationRequest? ConfirmationRequest { get; init; }

    /// <summary>
    /// The risk level that was evaluated.
    /// </summary>
    public RiskLevel RiskLevel { get; init; }

    /// <summary>
    /// Creates a result indicating confirmation is required.
    /// </summary>
    public static ConfirmationEvaluationResult RequireConfirmation(
        ConfirmationRequest request,
        double confidence,
        double threshold,
        RiskLevel riskLevel,
        string reason)
    {
        return new ConfirmationEvaluationResult
        {
            RequiresConfirmation = true,
            CanProceed = false,
            ConfirmationRequest = request,
            Confidence = confidence,
            Threshold = threshold,
            RiskLevel = riskLevel,
            Reason = reason
        };
    }

    /// <summary>
    /// Creates a result indicating the action can proceed without confirmation.
    /// </summary>
    public static ConfirmationEvaluationResult Allow(
        double confidence,
        double threshold,
        RiskLevel riskLevel,
        string reason)
    {
        return new ConfirmationEvaluationResult
        {
            RequiresConfirmation = false,
            CanProceed = true,
            Confidence = confidence,
            Threshold = threshold,
            RiskLevel = riskLevel,
            Reason = reason
        };
    }

    /// <summary>
    /// Creates a result indicating the action is blocked.
    /// </summary>
    public static ConfirmationEvaluationResult Block(
        string reason,
        double confidence = 0.0)
    {
        return new ConfirmationEvaluationResult
        {
            RequiresConfirmation = false,
            CanProceed = false,
            Confidence = confidence,
            Reason = reason
        };
    }
}

/// <summary>
/// Configuration options for confirmation gate evaluation.
/// </summary>
public sealed class ConfirmationOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "nirmataAgents:Confirmation";

    /// <summary>
    /// The confidence threshold for write operations. Below this requires confirmation.
    /// Default: 0.9
    /// </summary>
    public double ConfirmationThreshold { get; set; } = 0.9;

    /// <summary>
    /// Threshold for destructive operations (high bar due to irreversible nature).
    /// Default: 0.95
    /// </summary>
    public double DestructiveThreshold { get; set; } = 0.95;

    /// <summary>
    /// Threshold for write operations (file modifications, etc.).
    /// Default: 0.8
    /// </summary>
    public double WriteThreshold { get; set; } = 0.8;

    /// <summary>
    /// Threshold for ambiguous or unclear operations.
    /// Default: 0.7
    /// </summary>
    public double AmbiguousThreshold { get; set; } = 0.7;

    /// <summary>
    /// Whether to require confirmation for all write operations regardless of confidence.
    /// Default: false
    /// </summary>
    public bool AlwaysConfirmWrites { get; set; } = false;

    /// <summary>
    /// Timeout for confirmation requests in seconds.
    /// Default: 300 (5 minutes)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Commands that never require confirmation (explicit overrides).
    /// </summary>
    public HashSet<string> NoConfirmationCommands { get; set; } = new(StringComparer.OrdinalIgnoreCase)
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
        RiskLevel.Read => 0.0,
        _ => ConfirmationThreshold
    };

    /// <summary>
    /// Gets the timeout as a TimeSpan.
    /// </summary>
    public TimeSpan GetTimeout() => TimeSpan.FromSeconds(TimeoutSeconds);
}

/// <summary>
/// Evaluates gating results to determine if user confirmation is required.
/// Integrates with the gating engine workflow to provide confirmation gate functionality.
/// </summary>
public sealed class ConfirmationGateEvaluator : IConfirmationGateEvaluator
{
    private readonly IDestructivenessAnalyzer _destructivenessAnalyzer;
    private readonly IOptions<ConfirmationOptions> _options;
    private readonly Dictionary<string, ConfirmationRequest> _pendingConfirmations = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfirmationGateEvaluator"/> class.
    /// </summary>
    /// <param name="destructivenessAnalyzer">The destructiveness analyzer for risk assessment.</param>
    /// <param name="options">The confirmation options configuration.</param>
    public ConfirmationGateEvaluator(
        IDestructivenessAnalyzer destructivenessAnalyzer,
        IOptions<ConfirmationOptions> options)
    {
        _destructivenessAnalyzer = destructivenessAnalyzer;
        _options = options;
    }

    /// <inheritdoc />
    public ConfirmationEvaluationResult Evaluate(
        GatingResult gatingResult,
        GatingContext context,
        IntentClassificationResult classification)
    {
        var options = _options.Value;
        var intent = classification.Intent;
        var phase = gatingResult.TargetPhase;

        // Get risk level for the phase
        var riskLevel = _destructivenessAnalyzer.AnalyzeRisk(phase, context);

        // Read-only operations never require confirmation
        if (intent.SideEffect == SideEffect.None || intent.SideEffect == SideEffect.ReadOnly)
        {
            return ConfirmationEvaluationResult.Allow(
                intent.Confidence,
                0.0,
                riskLevel,
                "Read-only operation - no confirmation needed");
        }

        // Check for explicit no-confirmation commands
        if (classification.ParsedCommand?.CommandName is { } cmdName &&
            options.NoConfirmationCommands.Contains(cmdName))
        {
            return ConfirmationEvaluationResult.Allow(
                intent.Confidence,
                0.0,
                riskLevel,
                $"Command '{cmdName}' is in no-confirmation list");
        }

        // Determine the appropriate threshold based on risk level
        var threshold = options.GetThresholdForRiskLevel(riskLevel);

        // Always confirm writes if configured
        if (options.AlwaysConfirmWrites && intent.SideEffect == SideEffect.Write)
        {
            var request = CreateConfirmationRequest(classification, gatingResult, options, threshold, riskLevel);
            _pendingConfirmations[request.Id] = request;

            return ConfirmationEvaluationResult.RequireConfirmation(
                request,
                intent.Confidence,
                threshold,
                riskLevel,
                "Always confirm writes is enabled");
        }

        // Check if the destructiveness analyzer requires confirmation
        var requiresConfirmationFromAnalyzer = _destructivenessAnalyzer.RequiresConfirmation(phase, context);

        // Destructive operations always require confirmation
        if (requiresConfirmationFromAnalyzer || IsDestructiveRiskLevel(riskLevel))
        {
            var request = CreateConfirmationRequest(classification, gatingResult, options, threshold, riskLevel);
            _pendingConfirmations[request.Id] = request;

            return ConfirmationEvaluationResult.RequireConfirmation(
                request,
                intent.Confidence,
                threshold,
                riskLevel,
                $"Destructive operation ({riskLevel}) requires confirmation");
        }

        // Check confidence against threshold
        if (intent.Confidence >= threshold)
        {
            return ConfirmationEvaluationResult.Allow(
                intent.Confidence,
                threshold,
                riskLevel,
                $"Confidence ({intent.Confidence:F2}) meets threshold ({threshold:F2})");
        }

        // Confidence below threshold - require confirmation
        var confirmationRequest = CreateConfirmationRequest(classification, gatingResult, options, threshold, riskLevel);
        _pendingConfirmations[confirmationRequest.Id] = confirmationRequest;

        return ConfirmationEvaluationResult.RequireConfirmation(
            confirmationRequest,
            intent.Confidence,
            threshold,
            riskLevel,
            $"Confidence ({intent.Confidence:F2}) below threshold ({threshold:F2})");
    }

    /// <inheritdoc />
    public bool EvaluateResponse(string confirmationId, ConfirmationResponse response)
    {
        if (!_pendingConfirmations.TryGetValue(confirmationId, out var request))
        {
            return false;
        }

        _pendingConfirmations.Remove(confirmationId);

        // Check for timeout
        if (request.Timeout.HasValue &&
            DateTimeOffset.UtcNow - request.RequestedAt > request.Timeout.Value)
        {
            return false;
        }

        return response.Confirmed;
    }

    /// <summary>
    /// Gets a pending confirmation by ID.
    /// </summary>
    public ConfirmationRequest? GetPendingConfirmation(string id)
    {
        _pendingConfirmations.TryGetValue(id, out var request);
        return request;
    }

    /// <summary>
    /// Gets all pending confirmations.
    /// </summary>
    public IReadOnlyDictionary<string, ConfirmationRequest> GetPendingConfirmations()
    {
        return _pendingConfirmations;
    }

    /// <summary>
    /// Removes expired confirmations and returns the expired ones.
    /// </summary>
    public IReadOnlyList<ConfirmationRequest> RemoveExpiredConfirmations()
    {
        var expired = new List<ConfirmationRequest>();
        var now = DateTimeOffset.UtcNow;

        foreach (var kvp in _pendingConfirmations.ToList())
        {
            if (kvp.Value.Timeout.HasValue &&
                now - kvp.Value.RequestedAt > kvp.Value.Timeout.Value)
            {
                expired.Add(kvp.Value);
                _pendingConfirmations.Remove(kvp.Key);
            }
        }

        return expired;
    }

    private static bool IsDestructiveRiskLevel(RiskLevel riskLevel)
    {
        return riskLevel is RiskLevel.WriteDestructive
            or RiskLevel.WriteDestructiveGit
            or RiskLevel.WorkspaceDestructive;
    }

    private static ConfirmationRequest CreateConfirmationRequest(
        IntentClassificationResult classification,
        GatingResult gatingResult,
        ConfirmationOptions options,
        double threshold,
        RiskLevel riskLevel)
    {
        var intent = classification.Intent;
        var proposedAction = gatingResult.ProposedAction;

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
            ActionDescription = BuildActionDescription(proposedAction, intent),
            Confidence = intent.Confidence,
            Threshold = threshold,
            Timeout = options.GetTimeout(),
            Context = new Dictionary<string, object>
            {
                ["phase"] = gatingResult.TargetPhase,
                ["riskLevel"] = riskLevel.ToString(),
                ["sideEffect"] = intent.SideEffect.ToString(),
                ["affectedResources"] = proposedAction?.AffectedResources ?? Array.Empty<string>(),
                ["requiresConfirmation"] = true
            }
        };
    }

    private static string BuildActionDescription(ProposedAction? proposedAction, Intent intent)
    {
        if (proposedAction != null)
        {
            return $"{proposedAction.Description} ({proposedAction.RiskLevel} risk)";
        }

        return $"Execute workflow action: {intent.Kind} ({intent.SideEffect} side effect)";
    }
}
