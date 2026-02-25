namespace Gmsd.Web.Models.Streaming;

/// <summary>
/// Emits orchestration events for classification, gating, dispatch, and execution phases.
/// Ensures consistent event emission across all orchestration steps.
/// </summary>
public class OrchestrationEventEmitter : IOrchestrationEventEmitter
{
    private readonly IEventSink _eventSink;
    private string? _correlationId;
    private long _sequenceNumber;

    public OrchestrationEventEmitter(IEventSink eventSink)
    {
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        _correlationId = Guid.NewGuid().ToString("N");
        _sequenceNumber = 0;
    }

    /// <summary>
    /// Sets the correlation ID for all subsequent events.
    /// </summary>
    public void SetCorrelationId(string correlationId)
    {
        _correlationId = correlationId;
    }

    /// <summary>
    /// Emits a classification event when intent is classified.
    /// </summary>
    public async ValueTask<bool> EmitClassificationEventAsync(
        string category,
        double confidence,
        string? reasoning = null,
        string? userInput = null,
        CancellationToken cancellationToken = default)
    {
        return await _eventSink.EmitIntentClassifiedAsync(
            category,
            confidence,
            reasoning,
            userInput,
            _correlationId,
            IncrementSequence(),
            cancellationToken);
    }

    /// <summary>
    /// Emits a gating event when a phase is selected.
    /// </summary>
    public async ValueTask<bool> EmitGatingEventAsync(
        string phase,
        string? reasoning = null,
        bool requiresConfirmation = false,
        ProposedAction? proposedAction = null,
        CancellationToken cancellationToken = default)
    {
        return await _eventSink.EmitGateSelectedAsync(
            phase,
            reasoning,
            requiresConfirmation,
            proposedAction,
            _correlationId,
            IncrementSequence(),
            cancellationToken);
    }

    /// <summary>
    /// Emits a dispatch start event.
    /// </summary>
    public async ValueTask<bool> EmitDispatchStartAsync(
        string phase,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        return await _eventSink.EmitPhaseLifecycleAsync(
            phase,
            "started",
            context,
            null,
            null,
            _correlationId,
            IncrementSequence(),
            cancellationToken);
    }

    /// <summary>
    /// Emits a dispatch completion event.
    /// </summary>
    public async ValueTask<bool> EmitDispatchCompleteAsync(
        string phase,
        bool success,
        List<PhaseArtifact>? artifacts = null,
        PhaseError? error = null,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        return await _eventSink.EmitPhaseLifecycleAsync(
            phase,
            "completed",
            context,
            artifacts,
            error,
            _correlationId,
            IncrementSequence(),
            cancellationToken);
    }

    /// <summary>
    /// Emits a tool call event.
    /// </summary>
    public async ValueTask<bool> EmitToolCallAsync(
        string callId,
        string toolName,
        Dictionary<string, object>? parameters = null,
        string? phaseContext = null,
        CancellationToken cancellationToken = default)
    {
        return await _eventSink.EmitToolCallAsync(
            callId,
            toolName,
            parameters,
            phaseContext,
            _correlationId,
            IncrementSequence(),
            cancellationToken);
    }

    /// <summary>
    /// Emits a tool result event.
    /// </summary>
    public async ValueTask<bool> EmitToolResultAsync(
        string callId,
        bool success,
        object? result = null,
        string? error = null,
        long durationMs = 0,
        CancellationToken cancellationToken = default)
    {
        return await _eventSink.EmitToolResultAsync(
            callId,
            success,
            result,
            error,
            durationMs,
            _correlationId,
            IncrementSequence(),
            cancellationToken);
    }

    /// <summary>
    /// Emits a run lifecycle event.
    /// </summary>
    public async ValueTask<bool> EmitRunLifecycleAsync(
        string status,
        string? runId = null,
        long? durationMs = null,
        bool? success = null,
        List<string>? artifactReferences = null,
        CancellationToken cancellationToken = default)
    {
        return await _eventSink.EmitRunLifecycleAsync(
            status,
            runId,
            durationMs,
            success,
            artifactReferences,
            _correlationId,
            IncrementSequence(),
            cancellationToken);
    }

    /// <summary>
    /// Emits an error event.
    /// </summary>
    public async ValueTask<bool> EmitErrorAsync(
        string severity,
        string code,
        string message,
        string? context = null,
        bool recoverable = false,
        string? retryAction = null,
        CancellationToken cancellationToken = default)
    {
        return await _eventSink.EmitErrorAsync(
            severity,
            code,
            message,
            context,
            recoverable,
            retryAction,
            _correlationId,
            IncrementSequence(),
            cancellationToken);
    }

    /// <summary>
    /// Emits tool calling loop events.
    /// </summary>
    public async ValueTask<bool> EmitToolCallDetectedAsync(
        int iteration,
        List<ToolCallInfo> toolCalls,
        CancellationToken cancellationToken = default)
    {
        var payload = new ToolCallDetectedPayload
        {
            Iteration = iteration,
            ToolCalls = toolCalls
        };

        return await _eventSink.EmitAsync(
            StreamingEventType.ToolCallDetected,
            payload,
            _correlationId,
            IncrementSequence(),
            cancellationToken);
    }

    /// <summary>
    /// Emits a tool call started event.
    /// </summary>
    public async ValueTask<bool> EmitToolCallStartedAsync(
        int iteration,
        string toolCallId,
        string toolName,
        string argumentsJson,
        CancellationToken cancellationToken = default)
    {
        var payload = new ToolCallStartedPayload
        {
            Iteration = iteration,
            ToolCallId = toolCallId,
            ToolName = toolName,
            ArgumentsJson = argumentsJson
        };

        return await _eventSink.EmitAsync(
            StreamingEventType.ToolCallStarted,
            payload,
            _correlationId,
            IncrementSequence(),
            cancellationToken);
    }

    /// <summary>
    /// Emits a tool call completed event.
    /// </summary>
    public async ValueTask<bool> EmitToolCallCompletedAsync(
        int iteration,
        string toolCallId,
        string toolName,
        long durationMs,
        bool hasResult,
        string? resultSummary = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new ToolCallCompletedPayload
        {
            Iteration = iteration,
            ToolCallId = toolCallId,
            ToolName = toolName,
            DurationMs = durationMs,
            HasResult = hasResult,
            ResultSummary = resultSummary
        };

        return await _eventSink.EmitAsync(
            StreamingEventType.ToolCallCompleted,
            payload,
            _correlationId,
            IncrementSequence(),
            cancellationToken);
    }

    /// <summary>
    /// Emits a tool call failed event.
    /// </summary>
    public async ValueTask<bool> EmitToolCallFailedAsync(
        int iteration,
        string toolCallId,
        string toolName,
        string errorCode,
        string errorMessage,
        long durationMs,
        CancellationToken cancellationToken = default)
    {
        var payload = new ToolCallFailedPayload
        {
            Iteration = iteration,
            ToolCallId = toolCallId,
            ToolName = toolName,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            DurationMs = durationMs
        };

        return await _eventSink.EmitAsync(
            StreamingEventType.ToolCallFailed,
            payload,
            _correlationId,
            IncrementSequence(),
            cancellationToken);
    }

    /// <summary>
    /// Emits a tool results submitted event.
    /// </summary>
    public async ValueTask<bool> EmitToolResultsSubmittedAsync(
        int iteration,
        List<ToolResultInfo> results,
        CancellationToken cancellationToken = default)
    {
        var payload = new ToolResultsSubmittedPayload
        {
            Iteration = iteration,
            ResultCount = results.Count,
            Results = results
        };

        return await _eventSink.EmitAsync(
            StreamingEventType.ToolResultsSubmitted,
            payload,
            _correlationId,
            IncrementSequence(),
            cancellationToken);
    }

    /// <summary>
    /// Emits a tool loop iteration completed event.
    /// </summary>
    public async ValueTask<bool> EmitToolLoopIterationCompletedAsync(
        int iteration,
        bool hasMoreToolCalls,
        int toolCallCount,
        long durationMs,
        CancellationToken cancellationToken = default)
    {
        var payload = new ToolLoopIterationCompletedPayload
        {
            Iteration = iteration,
            HasMoreToolCalls = hasMoreToolCalls,
            ToolCallCount = toolCallCount,
            DurationMs = durationMs
        };

        return await _eventSink.EmitAsync(
            StreamingEventType.ToolLoopIterationCompleted,
            payload,
            _correlationId,
            IncrementSequence(),
            cancellationToken);
    }

    /// <summary>
    /// Emits a tool loop completed event.
    /// </summary>
    public async ValueTask<bool> EmitToolLoopCompletedAsync(
        int totalIterations,
        int totalToolCalls,
        string completionReason,
        long totalDurationMs,
        CancellationToken cancellationToken = default)
    {
        var payload = new ToolLoopCompletedPayload
        {
            TotalIterations = totalIterations,
            TotalToolCalls = totalToolCalls,
            CompletionReason = completionReason,
            TotalDurationMs = totalDurationMs
        };

        return await _eventSink.EmitAsync(
            StreamingEventType.ToolLoopCompleted,
            payload,
            _correlationId,
            IncrementSequence(),
            cancellationToken);
    }

    /// <summary>
    /// Emits a tool loop failed event.
    /// </summary>
    public async ValueTask<bool> EmitToolLoopFailedAsync(
        string errorCode,
        string errorMessage,
        int iteration,
        CancellationToken cancellationToken = default)
    {
        var payload = new ToolLoopFailedPayload
        {
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Iteration = iteration
        };

        return await _eventSink.EmitAsync(
            StreamingEventType.ToolLoopFailed,
            payload,
            _correlationId,
            IncrementSequence(),
            cancellationToken);
    }

    private long IncrementSequence()
    {
        return _sequenceNumber++;
    }
}
