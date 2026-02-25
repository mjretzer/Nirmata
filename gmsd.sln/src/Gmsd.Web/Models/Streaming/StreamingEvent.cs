namespace Gmsd.Web.Models.Streaming;

/// <summary>
/// Base envelope class for all streaming events.
/// Wraps event metadata and payload for transmission via SSE.
/// </summary>
public class StreamingEvent
{
    /// <summary>
    /// Unique identifier for this event
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The type of event being emitted
    /// </summary>
    public StreamingEventType Type { get; set; }

    /// <summary>
    /// ISO 8601 timestamp when the event was emitted
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optional correlation ID for tracing events across a conversation
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Sequence number for ordering events (optional but recommended)
    /// </summary>
    public long? SequenceNumber { get; set; }

    /// <summary>
    /// The event payload - varies by event type
    /// </summary>
    public object? Payload { get; set; }

    /// <summary>
    /// Creates a streaming event with the specified type and payload
    /// </summary>
    public static StreamingEvent Create<TPayload>(
        StreamingEventType type,
        TPayload payload,
        string? correlationId = null,
        long? sequenceNumber = null) where TPayload : class
    {
        return new StreamingEvent
        {
            Type = type,
            Payload = payload,
            CorrelationId = correlationId,
            SequenceNumber = sequenceNumber
        };
    }
}
