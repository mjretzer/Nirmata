using Gmsd.Agents.Execution.ToolCalling;
using Gmsd.Aos.Contracts.Tools;
using Gmsd.Web.Models.Streaming;

namespace Gmsd.Web.AgentRunner;

/// <summary>
/// Adapter that bridges tool execution events to streaming events.
/// Implements both the legacy IToolEventSink and the new IToolCallingEventEmitter (Task 8.2).
/// </summary>
public sealed class StreamingToolEventSink : IToolEventSink, IToolCallingEventEmitter
{
    private readonly IEventSink _eventSink;
    private long _sequenceNumber;

    /// <summary>
    /// Creates a new streaming tool event sink.
    /// </summary>
    /// <param name="eventSink">The underlying event sink to emit events to</param>
    /// <param name="startingSequence">Optional starting sequence number</param>
    public StreamingToolEventSink(IEventSink eventSink, long startingSequence = 0)
    {
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        _sequenceNumber = startingSequence;
    }

    /// <inheritdoc />
    public void EmitToolCall(
        string callId,
        string toolName,
        Dictionary<string, object>? parameters = null,
        string? phaseContext = null,
        string? correlationId = null)
    {
        var payload = new ToolCallPayload
        {
            CallId = callId,
            ToolName = toolName,
            Parameters = parameters,
            PhaseContext = phaseContext
        };

        _eventSink.TryEmit(StreamingEventType.ToolCall, payload, correlationId, Interlocked.Increment(ref _sequenceNumber));
    }

    /// <inheritdoc />
    public void EmitToolResult(
        string callId,
        bool success,
        object? result = null,
        string? error = null,
        long durationMs = 0,
        string? correlationId = null)
    {
        var payload = new ToolResultPayload
        {
            CallId = callId,
            Success = success,
            Result = result,
            Error = error,
            DurationMs = durationMs
        };

        _eventSink.TryEmit(StreamingEventType.ToolResult, payload, correlationId, Interlocked.Increment(ref _sequenceNumber));
    }

    /// <inheritdoc />
    public void Emit(ToolCallingEvent @event)
    {
        var streamingEvent = ToolCallingEventAdapter.ToStreamingEvent(@event);
        streamingEvent.SequenceNumber = Interlocked.Increment(ref _sequenceNumber);
        _eventSink.TryEmit(streamingEvent);
    }
}
