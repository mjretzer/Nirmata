using nirmata.Agents.Execution.ControlPlane;
using nirmata.Agents.Execution.Preflight;
using nirmata.Agents.Execution.Preflight.CommandSuggestion;

namespace nirmata.Agents.Tests.Fakes;

/// <summary>
/// Fake implementation of IConfirmationGate for testing.
/// Allows tests to control confirmation behavior.
/// </summary>
public sealed class FakeConfirmationGate : IConfirmationGate
{
    private readonly Dictionary<string, ConfirmationRequest> _pendingConfirmations = new();
    private bool _shouldRequireConfirmation;
    private bool _autoConfirm;
    private RiskLevel? _detectedGitRisk;
    private RiskLevel? _detectedFileSystemRisk;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeConfirmationGate"/> class.
    /// </summary>
    public FakeConfirmationGate()
    {
    }

    /// <summary>
    /// Sets whether the gate should require confirmation for all evaluations.
    /// </summary>
    public void SetRequireConfirmation(bool require) => _shouldRequireConfirmation = require;

    /// <summary>
    /// Sets whether to auto-confirm (true) or auto-reject (false) when confirmation is required.
    /// </summary>
    public void SetAutoConfirm(bool autoConfirm) => _autoConfirm = autoConfirm;

    /// <summary>
    /// Sets the git operation risk level to return from DetectGitOperationRisk.
    /// </summary>
    public void SetDetectedGitRisk(RiskLevel? riskLevel) => _detectedGitRisk = riskLevel;

    /// <summary>
    /// Sets the file system scope risk level to return from DetectFileSystemScopeRisk.
    /// </summary>
    public void SetDetectedFileSystemRisk(RiskLevel? riskLevel) => _detectedFileSystemRisk = riskLevel;

    /// <inheritdoc />
    public ConfirmationGateResult Evaluate(IntentClassificationResult classification, ConfirmationGateOptions? options = null)
    {
        options ??= new ConfirmationGateOptions();

        // If not requiring confirmation, allow immediately
        if (!_shouldRequireConfirmation)
        {
            return ConfirmationGateResult.Allow("Fake gate allows without confirmation", 1.0);
        }

        // Create confirmation request
        var request = new ConfirmationRequest
        {
            OriginalInput = classification.ParsedCommand?.RawInput ?? "test-input",
            ClassifiedIntent = new IntentClassifiedPayload
            {
                Category = classification.Intent.Kind.ToString(),
                Confidence = classification.Intent.Confidence,
                Reasoning = classification.Intent.Reasoning,
                UserInput = classification.ParsedCommand?.RawInput
            },
            ActionDescription = $"Execute {classification.Intent.Kind} action",
            Confidence = classification.Intent.Confidence,
            Threshold = options.ConfirmationThreshold
        };

        _pendingConfirmations[request.Id] = request;

        return ConfirmationGateResult.RequireConfirmation(request, classification.Intent.Confidence);
    }

    /// <inheritdoc />
    public ConfirmationGateResult EvaluateSuggestion(IntentClassificationResult classification, CommandProposal proposal, ConfirmationGateOptions? options = null)
    {
        options ??= new ConfirmationGateOptions();

        var request = new ConfirmationRequest
        {
            OriginalInput = proposal.FormattedCommand ?? "suggested-command",
            ClassifiedIntent = new IntentClassifiedPayload
            {
                Category = "SuggestedCommand",
                Confidence = proposal.Confidence,
                Reasoning = proposal.Reasoning,
                UserInput = proposal.FormattedCommand
            },
            ActionDescription = $"Execute suggested command: {proposal.CommandName}",
            Confidence = proposal.Confidence,
            Threshold = options.ConfirmationThreshold
        };

        _pendingConfirmations[request.Id] = request;

        return ConfirmationGateResult.RequireConfirmation(request, proposal.Confidence);
    }

    /// <inheritdoc />
    public bool ProcessResponse(ConfirmationResponse response)
    {
        if (!_pendingConfirmations.TryGetValue(response.RequestId, out var request))
        {
            return false;
        }

        _pendingConfirmations.Remove(response.RequestId);

        // Check for timeout
        if (request.Timeout.HasValue &&
            DateTimeOffset.UtcNow - request.RequestedAt > request.Timeout.Value)
        {
            return false;
        }

        return response.Confirmed;
    }

    /// <inheritdoc />
    public PrerequisiteCheckResult EvaluatePrerequisites(string workspaceRoot)
    {
        // Fake always returns satisfied for tests
        return PrerequisiteCheckResult.Satisfied();
    }

    /// <inheritdoc />
    public ConfirmationGateResult EvaluateWithDestructiveness(
        IntentClassificationResult classification,
        string phase,
        GatingContext gatingContext,
        ConfirmationGateOptions? options = null)
    {
        // Delegate to standard Evaluate for fake implementation
        return Evaluate(classification, options);
    }

    /// <inheritdoc />
    public RiskLevel? DetectGitOperationRisk(IReadOnlyList<string> arguments)
    {
        return _detectedGitRisk;
    }

    /// <inheritdoc />
    public RiskLevel? DetectFileSystemScopeRisk(IntentClassificationResult classification, string phase)
    {
        return _detectedFileSystemRisk;
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
    /// Clears all pending confirmations.
    /// </summary>
    public void Clear() => _pendingConfirmations.Clear();
}
