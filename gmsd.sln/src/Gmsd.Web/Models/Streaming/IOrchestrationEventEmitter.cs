namespace Gmsd.Web.Models.Streaming;

/// <summary>
/// Defines the contract for emitting orchestration events.
/// Ensures consistent event emission across all orchestration phases.
/// </summary>
public interface IOrchestrationEventEmitter
{
    /// <summary>
    /// Sets the correlation ID for all subsequent events.
    /// </summary>
    void SetCorrelationId(string correlationId);

    /// <summary>
    /// Emits a classification event when intent is classified.
    /// </summary>
    ValueTask<bool> EmitClassificationEventAsync(
        string category,
        double confidence,
        string? reasoning = null,
        string? userInput = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Emits a gating event when a phase is selected.
    /// </summary>
    ValueTask<bool> EmitGatingEventAsync(
        string phase,
        string? reasoning = null,
        bool requiresConfirmation = false,
        ProposedAction? proposedAction = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Emits a dispatch start event.
    /// </summary>
    ValueTask<bool> EmitDispatchStartAsync(
        string phase,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Emits a dispatch completion event.
    /// </summary>
    ValueTask<bool> EmitDispatchCompleteAsync(
        string phase,
        bool success,
        List<PhaseArtifact>? artifacts = null,
        PhaseError? error = null,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Emits a tool call event.
    /// </summary>
    ValueTask<bool> EmitToolCallAsync(
        string callId,
        string toolName,
        Dictionary<string, object>? parameters = null,
        string? phaseContext = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Emits a tool result event.
    /// </summary>
    ValueTask<bool> EmitToolResultAsync(
        string callId,
        bool success,
        object? result = null,
        string? error = null,
        long durationMs = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Emits a run lifecycle event.
    /// </summary>
    ValueTask<bool> EmitRunLifecycleAsync(
        string status,
        string? runId = null,
        long? durationMs = null,
        bool? success = null,
        List<string>? artifactReferences = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Emits an error event.
    /// </summary>
    ValueTask<bool> EmitErrorAsync(
        string severity,
        string code,
        string message,
        string? context = null,
        bool recoverable = false,
        string? retryAction = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Emits tool calling loop events.
    /// </summary>
    ValueTask<bool> EmitToolCallDetectedAsync(
        int iteration,
        List<ToolCallInfo> toolCalls,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Emits a tool call started event.
    /// </summary>
    ValueTask<bool> EmitToolCallStartedAsync(
        int iteration,
        string toolCallId,
        string toolName,
        string argumentsJson,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Emits a tool call completed event.
    /// </summary>
    ValueTask<bool> EmitToolCallCompletedAsync(
        int iteration,
        string toolCallId,
        string toolName,
        long durationMs,
        bool hasResult,
        string? resultSummary = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Emits a tool call failed event.
    /// </summary>
    ValueTask<bool> EmitToolCallFailedAsync(
        int iteration,
        string toolCallId,
        string toolName,
        string errorCode,
        string errorMessage,
        long durationMs,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Emits a tool results submitted event.
    /// </summary>
    ValueTask<bool> EmitToolResultsSubmittedAsync(
        int iteration,
        List<ToolResultInfo> results,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Emits a tool loop iteration completed event.
    /// </summary>
    ValueTask<bool> EmitToolLoopIterationCompletedAsync(
        int iteration,
        bool hasMoreToolCalls,
        int toolCallCount,
        long durationMs,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Emits a tool loop completed event.
    /// </summary>
    ValueTask<bool> EmitToolLoopCompletedAsync(
        int totalIterations,
        int totalToolCalls,
        string completionReason,
        long totalDurationMs,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Emits a tool loop failed event.
    /// </summary>
    ValueTask<bool> EmitToolLoopFailedAsync(
        string errorCode,
        string errorMessage,
        int iteration,
        CancellationToken cancellationToken = default);
}
