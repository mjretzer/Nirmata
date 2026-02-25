namespace Gmsd.Web.Models.Streaming;

/// <summary>
/// Extension methods for common event emission patterns using IEventSink.
/// Provides convenient helpers for emitting typed events without manually constructing payloads.
/// </summary>
public static class EventSinkExtensions
{
    /// <summary>
    /// Emits a command.suggested event.
    /// </summary>
    /// <param name="sink">The event sink</param>
    /// <param name="commandName">The name of the suggested command</param>
    /// <param name="arguments">Arguments for the suggested command</param>
    /// <param name="formattedCommand">The fully formatted command string</param>
    /// <param name="confidence">Confidence score between 0.0 and 1.0</param>
    /// <param name="reasoning">Reasoning for the suggestion</param>
    /// <param name="originalInput">The original user input</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <param name="sequenceNumber">Optional sequence number</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static ValueTask<bool> EmitCommandSuggestedAsync(
        this IEventSink sink,
        string commandName,
        string[]? arguments = null,
        string? formattedCommand = null,
        double confidence = 0.0,
        string? reasoning = null,
        string? originalInput = null,
        string? correlationId = null,
        long? sequenceNumber = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new CommandSuggestedPayload
        {
            CommandName = commandName,
            Arguments = arguments,
            FormattedCommand = formattedCommand,
            Confidence = confidence,
            Reasoning = reasoning,
            OriginalInput = originalInput
        };

        return sink.EmitAsync(StreamingEventType.CommandSuggested, payload, correlationId, sequenceNumber, cancellationToken);
    }

    /// <summary>
    /// Emits a command.suggested event synchronously.
    /// </summary>
    public static bool TryEmitCommandSuggested(
        this IEventSink sink,
        string commandName,
        string[]? arguments = null,
        string? formattedCommand = null,
        double confidence = 0.0,
        string? reasoning = null,
        string? originalInput = null,
        string? correlationId = null,
        long? sequenceNumber = null)
    {
        var payload = new CommandSuggestedPayload
        {
            CommandName = commandName,
            Arguments = arguments,
            FormattedCommand = formattedCommand,
            Confidence = confidence,
            Reasoning = reasoning,
            OriginalInput = originalInput
        };

        return sink.TryEmit(StreamingEventType.CommandSuggested, payload, correlationId, sequenceNumber);
    }

    /// <summary>
    /// Emits an intent.classified event.
    /// </summary>
    /// <param name="sink">The event sink</param>
    /// <param name="category">The classified intent category</param>
    /// <param name="confidence">Confidence score between 0.0 and 1.0</param>
    /// <param name="reasoning">Reasoning for the classification</param>
    /// <param name="userInput">The original user input</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <param name="sequenceNumber">Optional sequence number</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static ValueTask<bool> EmitIntentClassifiedAsync(
        this IEventSink sink,
        string category,
        double confidence,
        string? reasoning = null,
        string? userInput = null,
        string? correlationId = null,
        long? sequenceNumber = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new IntentClassifiedPayload
        {
            Category = category,
            Confidence = confidence,
            Reasoning = reasoning,
            UserInput = userInput
        };

        return sink.EmitAsync(StreamingEventType.IntentClassified, payload, correlationId, sequenceNumber, cancellationToken);
    }

    /// <summary>
    /// Emits a gate.selected event.
    /// </summary>
    /// <param name="sink">The event sink</param>
    /// <param name="phase">The selected phase</param>
    /// <param name="reasoning">Reasoning for the selection</param>
    /// <param name="requiresConfirmation">Whether confirmation is required</param>
    /// <param name="proposedAction">Proposed action details if confirmation needed</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <param name="sequenceNumber">Optional sequence number</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static ValueTask<bool> EmitGateSelectedAsync(
        this IEventSink sink,
        string phase,
        string? reasoning = null,
        bool requiresConfirmation = false,
        ProposedAction? proposedAction = null,
        string? correlationId = null,
        long? sequenceNumber = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new GateSelectedPayload
        {
            Phase = phase,
            Reasoning = reasoning,
            RequiresConfirmation = requiresConfirmation,
            ProposedAction = proposedAction
        };

        return sink.EmitAsync(StreamingEventType.GateSelected, payload, correlationId, sequenceNumber, cancellationToken);
    }

    /// <summary>
    /// Emits a tool.call event.
    /// </summary>
    /// <param name="sink">The event sink</param>
    /// <param name="callId">Unique identifier for correlating call with result</param>
    /// <param name="toolName">The name of the tool being invoked</param>
    /// <param name="parameters">Parameters passed to the tool</param>
    /// <param name="phaseContext">Phase context for the tool call</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <param name="sequenceNumber">Optional sequence number</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static ValueTask<bool> EmitToolCallAsync(
        this IEventSink sink,
        string callId,
        string toolName,
        Dictionary<string, object>? parameters = null,
        string? phaseContext = null,
        string? correlationId = null,
        long? sequenceNumber = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new ToolCallPayload
        {
            CallId = callId,
            ToolName = toolName,
            Parameters = parameters,
            PhaseContext = phaseContext
        };

        return sink.EmitAsync(StreamingEventType.ToolCall, payload, correlationId, sequenceNumber, cancellationToken);
    }

    /// <summary>
    /// Emits a tool.result event.
    /// </summary>
    /// <param name="sink">The event sink</param>
    /// <param name="callId">Unique identifier matching the original tool.call</param>
    /// <param name="success">Whether the tool execution succeeded</param>
    /// <param name="result">Result data from tool execution</param>
    /// <param name="error">Error message if execution failed</param>
    /// <param name="durationMs">Duration of tool execution in milliseconds</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <param name="sequenceNumber">Optional sequence number</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static ValueTask<bool> EmitToolResultAsync(
        this IEventSink sink,
        string callId,
        bool success,
        object? result = null,
        string? error = null,
        long durationMs = 0,
        string? correlationId = null,
        long? sequenceNumber = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new ToolResultPayload
        {
            CallId = callId,
            Success = success,
            Result = result,
            Error = error,
            DurationMs = durationMs
        };

        return sink.EmitAsync(StreamingEventType.ToolResult, payload, correlationId, sequenceNumber, cancellationToken);
    }

    /// <summary>
    /// Emits a phase.lifecycle event.
    /// </summary>
    /// <param name="sink">The event sink</param>
    /// <param name="phase">The phase name</param>
    /// <param name="status">"started" or "completed"</param>
    /// <param name="context">Contextual information</param>
    /// <param name="artifacts">Output artifacts (for completed events)</param>
    /// <param name="error">Error information if phase failed</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <param name="sequenceNumber">Optional sequence number</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static ValueTask<bool> EmitPhaseLifecycleAsync(
        this IEventSink sink,
        string phase,
        string status,
        Dictionary<string, object>? context = null,
        List<PhaseArtifact>? artifacts = null,
        PhaseError? error = null,
        string? correlationId = null,
        long? sequenceNumber = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new PhaseLifecyclePayload
        {
            Phase = phase,
            Status = status,
            Context = context,
            Artifacts = artifacts,
            Error = error
        };

        return sink.EmitAsync(StreamingEventType.PhaseLifecycle, payload, correlationId, sequenceNumber, cancellationToken);
    }

    /// <summary>
    /// Emits an assistant.delta event (streaming token).
    /// </summary>
    /// <param name="sink">The event sink</param>
    /// <param name="messageId">Unique identifier for the message being streamed</param>
    /// <param name="content">The token chunk content</param>
    /// <param name="index">Position in the overall message</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <param name="sequenceNumber">Optional sequence number</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static ValueTask<bool> EmitAssistantDeltaAsync(
        this IEventSink sink,
        string messageId,
        string content,
        int index = 0,
        string? correlationId = null,
        long? sequenceNumber = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new AssistantDeltaPayload
        {
            MessageId = messageId,
            Content = content,
            Index = index
        };

        return sink.EmitAsync(StreamingEventType.AssistantDelta, payload, correlationId, sequenceNumber, cancellationToken);
    }

    /// <summary>
    /// Emits an assistant.final event.
    /// </summary>
    /// <param name="sink">The event sink</param>
    /// <param name="messageId">Unique identifier matching the delta stream</param>
    /// <param name="content">The complete final content</param>
    /// <param name="structuredData">Structured data if response contains rich content</param>
    /// <param name="contentType">Content type of the response</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <param name="sequenceNumber">Optional sequence number</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static ValueTask<bool> EmitAssistantFinalAsync(
        this IEventSink sink,
        string messageId,
        string? content = null,
        object? structuredData = null,
        string? contentType = null,
        string? correlationId = null,
        long? sequenceNumber = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new AssistantFinalPayload
        {
            MessageId = messageId,
            Content = content,
            StructuredData = structuredData,
            ContentType = contentType
        };

        return sink.EmitAsync(StreamingEventType.AssistantFinal, payload, correlationId, sequenceNumber, cancellationToken);
    }

    /// <summary>
    /// Emits a run.lifecycle event.
    /// </summary>
    /// <param name="sink">The event sink</param>
    /// <param name="status">"started" or "finished"</param>
    /// <param name="runId">Unique identifier for the run</param>
    /// <param name="durationMs">Run duration in milliseconds (for finished events)</param>
    /// <param name="success">Whether the run completed successfully (for finished events)</param>
    /// <param name="artifactReferences">References to artifacts produced</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <param name="sequenceNumber">Optional sequence number</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static ValueTask<bool> EmitRunLifecycleAsync(
        this IEventSink sink,
        string status,
        string? runId = null,
        long? durationMs = null,
        bool? success = null,
        List<string>? artifactReferences = null,
        string? correlationId = null,
        long? sequenceNumber = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new RunLifecyclePayload
        {
            Status = status,
            RunId = runId,
            DurationMs = durationMs,
            Success = success,
            ArtifactReferences = artifactReferences
        };

        return sink.EmitAsync(StreamingEventType.RunLifecycle, payload, correlationId, sequenceNumber, cancellationToken);
    }

    /// <summary>
    /// Emits an error event.
    /// </summary>
    /// <param name="sink">The event sink</param>
    /// <param name="severity">Error severity level (error, warning, info)</param>
    /// <param name="code">Error code for categorization</param>
    /// <param name="message">Human-readable error message</param>
    /// <param name="context">Phase or component where error occurred</param>
    /// <param name="recoverable">Whether the error is recoverable</param>
    /// <param name="retryAction">Suggested retry action if recoverable</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <param name="sequenceNumber">Optional sequence number</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static ValueTask<bool> EmitErrorAsync(
        this IEventSink sink,
        string severity,
        string code,
        string message,
        string? context = null,
        bool recoverable = false,
        string? retryAction = null,
        string? correlationId = null,
        long? sequenceNumber = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new ErrorPayload
        {
            Severity = severity,
            Code = code,
            Message = message,
            Context = context,
            Recoverable = recoverable,
            RetryAction = retryAction
        };

        return sink.EmitAsync(StreamingEventType.Error, payload, correlationId, sequenceNumber, cancellationToken);
    }

    // Synchronous (TryEmit) versions for common events

    /// <summary>
    /// Emits an intent.classified event synchronously.
    /// </summary>
    public static bool TryEmitIntentClassified(
        this IEventSink sink,
        string category,
        double confidence,
        string? reasoning = null,
        string? userInput = null,
        string? correlationId = null,
        long? sequenceNumber = null)
    {
        var payload = new IntentClassifiedPayload
        {
            Category = category,
            Confidence = confidence,
            Reasoning = reasoning,
            UserInput = userInput
        };

        return sink.TryEmit(StreamingEventType.IntentClassified, payload, correlationId, sequenceNumber);
    }

    /// <summary>
    /// Emits a gate.selected event synchronously.
    /// </summary>
    public static bool TryEmitGateSelected(
        this IEventSink sink,
        string phase,
        string? reasoning = null,
        bool requiresConfirmation = false,
        ProposedAction? proposedAction = null,
        string? correlationId = null,
        long? sequenceNumber = null)
    {
        var payload = new GateSelectedPayload
        {
            Phase = phase,
            Reasoning = reasoning,
            RequiresConfirmation = requiresConfirmation,
            ProposedAction = proposedAction
        };

        return sink.TryEmit(StreamingEventType.GateSelected, payload, correlationId, sequenceNumber);
    }

    /// <summary>
    /// Emits an error event synchronously.
    /// </summary>
    public static bool TryEmitError(
        this IEventSink sink,
        string severity,
        string code,
        string message,
        string? context = null,
        bool recoverable = false,
        string? retryAction = null,
        string? correlationId = null,
        long? sequenceNumber = null)
    {
        var payload = new ErrorPayload
        {
            Severity = severity,
            Code = code,
            Message = message,
            Context = context,
            Recoverable = recoverable,
            RetryAction = retryAction
        };

        return sink.TryEmit(StreamingEventType.Error, payload, correlationId, sequenceNumber);
    }

    /// <summary>
    /// Emits a phase.lifecycle event synchronously.
    /// </summary>
    public static bool TryEmitPhaseLifecycle(
        this IEventSink sink,
        string phase,
        string status,
        Dictionary<string, object>? context = null,
        List<PhaseArtifact>? artifacts = null,
        PhaseError? error = null,
        string? correlationId = null,
        long? sequenceNumber = null)
    {
        var payload = new PhaseLifecyclePayload
        {
            Phase = phase,
            Status = status,
            Context = context,
            Artifacts = artifacts,
            Error = error
        };

        return sink.TryEmit(StreamingEventType.PhaseLifecycle, payload, correlationId, sequenceNumber);
    }
}
