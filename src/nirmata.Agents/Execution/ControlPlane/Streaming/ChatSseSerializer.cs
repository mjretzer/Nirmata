using System.Text.Json;

namespace nirmata.Agents.Execution.ControlPlane.Streaming;

/// <summary>
/// Serializes chat streaming events to Server-Sent Events (SSE) format.
/// </summary>
public static class ChatSseSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Serializes a chat streaming event to SSE format.
    /// Format: data: {json}\n\n
    /// </summary>
    public static string ToSseFormat(ChatStreamingEvent @event)
    {
        var json = JsonSerializer.Serialize(@event, @event.GetType(), JsonOptions);
        return $"data: {json}\n\n";
    }

    /// <summary>
    /// Creates a message_start SSE event.
    /// </summary>
    public static string CreateMessageStart(string messageId, string? correlationId = null, string? model = null)
    {
        var evt = new ChatMessageStartEvent
        {
            MessageId = messageId,
            CorrelationId = correlationId,
            Model = model
        };
        return ToSseFormat(evt);
    }

    /// <summary>
    /// Creates a content_delta SSE event.
    /// </summary>
    public static string CreateContentDelta(string messageId, string content, int index, bool isCompleteToken = false)
    {
        var evt = new ChatDeltaEvent
        {
            MessageId = messageId,
            Content = content,
            Index = index,
            IsCompleteToken = isCompleteToken
        };
        return ToSseFormat(evt);
    }

    /// <summary>
    /// Creates a message_complete SSE event.
    /// </summary>
    public static string CreateMessageComplete(
        string messageId,
        string? fullContent = null,
        int? tokenCount = null,
        long? durationMs = null,
        string? finishReason = "stop")
    {
        var evt = new ChatCompleteEvent
        {
            MessageId = messageId,
            FullContent = fullContent,
            TokenCount = tokenCount,
            DurationMs = durationMs,
            FinishReason = finishReason
        };
        return ToSseFormat(evt);
    }

    /// <summary>
    /// Creates an error SSE event.
    /// </summary>
    public static string CreateError(string code, string message, bool recoverable = false)
    {
        var evt = new ChatErrorEvent
        {
            Code = code,
            Message = message,
            Recoverable = recoverable
        };
        return ToSseFormat(evt);
    }
}
