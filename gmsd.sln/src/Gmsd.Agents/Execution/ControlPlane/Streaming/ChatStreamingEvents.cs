namespace Gmsd.Agents.Execution.ControlPlane.Streaming;

/// <summary>
/// Chat-specific SSE event types for streaming responses.
/// </summary>
public enum ChatStreamingEventType
{
    /// <summary>
    /// Initial event indicating chat streaming has started.
    /// </summary>
    MessageStart,

    /// <summary>
    /// Delta/chunk of the assistant's response content.
    /// </summary>
    ContentDelta,

    /// <summary>
    /// Final event indicating chat streaming has completed.
    /// </summary>
    MessageComplete,

    /// <summary>
    /// Error occurred during chat streaming.
    /// </summary>
    Error
}

/// <summary>
/// Base class for all chat streaming events.
/// </summary>
public abstract class ChatStreamingEvent
{
    /// <summary>
    /// Unique identifier for this event.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The type of chat streaming event.
    /// </summary>
    public ChatStreamingEventType EventType { get; init; }

    /// <summary>
    /// ISO 8601 timestamp when the event was emitted.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optional correlation ID for tracing events across a conversation.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Sequence number for ordering events.
    /// </summary>
    public long SequenceNumber { get; init; }
}

/// <summary>
/// Event indicating the start of a chat message stream.
/// </summary>
public sealed class ChatMessageStartEvent : ChatStreamingEvent
{
    public ChatMessageStartEvent()
    {
        EventType = ChatStreamingEventType.MessageStart;
    }

    /// <summary>
    /// Unique identifier for the message being streamed.
    /// </summary>
    public string MessageId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The role of the message sender (typically "assistant").
    /// </summary>
    public string Role { get; init; } = "assistant";

    /// <summary>
    /// Optional model or provider information.
    /// </summary>
    public string? Model { get; init; }
}

/// <summary>
/// Event containing a delta/chunk of the chat response content.
/// Corresponds to individual tokens or small token groups from the LLM.
/// </summary>
public sealed class ChatDeltaEvent : ChatStreamingEvent
{
    public ChatDeltaEvent()
    {
        EventType = ChatStreamingEventType.ContentDelta;
    }

    /// <summary>
    /// Unique identifier matching the message start event.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// The token chunk content. May be partial word/phrase.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Position in the overall message (0-indexed).
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// Whether this delta completes a word or sentence.
    /// </summary>
    public bool IsCompleteToken { get; init; }
}

/// <summary>
/// Event indicating the completion of a chat message stream.
/// </summary>
public sealed class ChatCompleteEvent : ChatStreamingEvent
{
    public ChatCompleteEvent()
    {
        EventType = ChatStreamingEventType.MessageComplete;
    }

    /// <summary>
    /// Unique identifier matching the message start event.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// The complete aggregated content of the response.
    /// </summary>
    public string? FullContent { get; init; }

    /// <summary>
    /// Total number of tokens in the response.
    /// </summary>
    public int? TokenCount { get; init; }

    /// <summary>
    /// Duration of the streaming response in milliseconds.
    /// </summary>
    public long? DurationMs { get; init; }

    /// <summary>
    /// Reason for completion (e.g., "stop", "length", "error").
    /// </summary>
    public string? FinishReason { get; init; } = "stop";
}

/// <summary>
/// Event indicating an error during chat streaming.
/// </summary>
public sealed class ChatErrorEvent : ChatStreamingEvent
{
    public ChatErrorEvent()
    {
        EventType = ChatStreamingEventType.Error;
    }

    /// <summary>
    /// Error severity level.
    /// </summary>
    public string Severity { get; init; } = "error";

    /// <summary>
    /// Error code for categorization.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Whether the error is recoverable.
    /// </summary>
    public bool Recoverable { get; init; }
}
