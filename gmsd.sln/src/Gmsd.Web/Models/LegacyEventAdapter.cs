#nullable disable
using System.Runtime.CompilerServices;
using Gmsd.Web.Controllers;
using Gmsd.Web.Models.Streaming;

namespace Gmsd.Web.Models;

/// <summary>
/// Adapter that transforms typed streaming events (v2) into legacy format (v1).
/// Provides backward compatibility for clients that expect the old event structure.
/// </summary>
public static class LegacyEventAdapter
{
    /// <summary>
    /// Transforms a typed StreamingEvent into legacy StreamingChatEvent format.
    /// </summary>
    /// <param name="event">The typed v2 event</param>
    /// <returns>A legacy v1 event</returns>
    public static StreamingChatEvent Transform(StreamingEvent @event)
    {
        return @event.Type switch
        {
            StreamingEventType.IntentClassified => TransformIntentClassified(@event),
            StreamingEventType.GateSelected => TransformGateSelected(@event),
            StreamingEventType.AssistantDelta => TransformAssistantDelta(@event),
            StreamingEventType.AssistantFinal => TransformAssistantFinal(@event),
            StreamingEventType.ToolCall => TransformToolCall(@event),
            StreamingEventType.ToolResult => TransformToolResult(@event),
            StreamingEventType.PhaseLifecycle => TransformPhaseLifecycle(@event),
            StreamingEventType.RunLifecycle => TransformRunLifecycle(@event),
            StreamingEventType.Error => TransformError(@event),
            _ => TransformDefault(@event)
        };
    }

    /// <summary>
    /// Transforms an async enumerable of v2 events to legacy format.
    /// </summary>
    public static async IAsyncEnumerable<StreamingChatEvent> TransformStream(
        IAsyncEnumerable<StreamingEvent> events,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var accumulatedContent = string.Empty;
        var currentMessageId = Guid.NewGuid().ToString("N");

        await foreach (var @event in events.WithCancellation(ct))
        {
            var legacyEvent = Transform(@event);

            // Accumulate content from delta events for final message
            if (@event.Type == StreamingEventType.AssistantDelta)
            {
                accumulatedContent += legacyEvent.Content;
                legacyEvent.MessageId = currentMessageId;
            }
            else if (@event.Type == StreamingEventType.AssistantFinal)
            {
                // Use accumulated content if the final event has empty content
                if (string.IsNullOrEmpty(legacyEvent.Content) && !string.IsNullOrEmpty(accumulatedContent))
                {
                    legacyEvent.Content = accumulatedContent;
                }
                legacyEvent.IsFinal = true;
                legacyEvent.MessageId = currentMessageId;
                accumulatedContent = string.Empty;
                currentMessageId = Guid.NewGuid().ToString("N");
            }
            else if (@event.Type == StreamingEventType.IntentClassified ||
                     @event.Type == StreamingEventType.GateSelected)
            {
                // Reasoning events become thinking messages
                legacyEvent.Type = "thinking";
                legacyEvent.MessageId = currentMessageId;
            }
            else
            {
                legacyEvent.MessageId = currentMessageId;
            }

            yield return legacyEvent;
        }
    }

    private static StreamingChatEvent TransformIntentClassified(StreamingEvent @event)
    {
        var payload = @event.Payload as IntentClassifiedPayload;
        var content = $"Intent classified: {payload?.Category ?? "Unknown"}";
        if (payload?.Confidence > 0)
        {
            content += $" (confidence: {payload.Confidence:P0})";
        }

        return new StreamingChatEvent
        {
            Type = "thinking",
            Content = content,
            MessageId = @event.Id,
            Timestamp = @event.Timestamp.UtcDateTime,
            Metadata = new Dictionary<string, object>
            {
                ["eventType"] = "intent.classified",
                ["category"] = payload?.Category,
                ["confidence"] = payload?.Confidence ?? 0,
                ["reasoning"] = payload?.Reasoning,
                ["correlationId"] = @event.CorrelationId
            }
        };
    }

    private static StreamingChatEvent TransformGateSelected(StreamingEvent @event)
    {
        var payload = @event.Payload as GateSelectedPayload;
        var content = $"Phase selected: {payload?.Phase ?? "Unknown"}";

        return new StreamingChatEvent
        {
            Type = "thinking",
            Content = content,
            MessageId = @event.Id,
            Timestamp = @event.Timestamp.UtcDateTime,
            Metadata = new Dictionary<string, object>
            {
                ["eventType"] = "gate.selected",
                ["phase"] = payload?.Phase,
                ["requiresConfirmation"] = payload?.RequiresConfirmation ?? false,
                ["correlationId"] = @event.CorrelationId
            }
        };
    }

    private static StreamingChatEvent TransformAssistantDelta(StreamingEvent @event)
    {
        var payload = @event.Payload as AssistantDeltaPayload;

        return new StreamingChatEvent
        {
            Type = "content_chunk",
            Content = payload?.Content ?? string.Empty,
            MessageId = @event.Id,
            IsFinal = false,
            Timestamp = @event.Timestamp.UtcDateTime,
            Metadata = new Dictionary<string, object>
            {
                ["eventType"] = "assistant.delta",
                ["index"] = payload?.Index ?? 0,
                ["messageId"] = payload?.MessageId,
                ["correlationId"] = @event.CorrelationId
            }
        };
    }

    private static StreamingChatEvent TransformAssistantFinal(StreamingEvent @event)
    {
        var payload = @event.Payload as AssistantFinalPayload;

        return new StreamingChatEvent
        {
            Type = "message_complete",
            Content = payload?.Content ?? string.Empty,
            MessageId = @event.Id,
            IsFinal = true,
            Timestamp = @event.Timestamp.UtcDateTime,
            Metadata = new Dictionary<string, object>
            {
                ["eventType"] = "assistant.final",
                ["contentType"] = payload?.ContentType ?? "text/plain",
                ["messageId"] = payload?.MessageId,
                ["correlationId"] = @event.CorrelationId
            }
        };
    }

    private static StreamingChatEvent TransformToolCall(StreamingEvent @event)
    {
        var payload = @event.Payload as ToolCallPayload;
        var content = $"🛠️ Calling tool: {payload?.ToolName ?? "Unknown"}";

        return new StreamingChatEvent
        {
            Type = "thinking",
            Content = content,
            MessageId = @event.Id,
            Timestamp = @event.Timestamp.UtcDateTime,
            Metadata = new Dictionary<string, object>
            {
                ["eventType"] = "tool.call",
                ["toolName"] = payload?.ToolName,
                ["callId"] = payload?.CallId,
                ["correlationId"] = @event.CorrelationId
            }
        };
    }

    private static StreamingChatEvent TransformToolResult(StreamingEvent @event)
    {
        var payload = @event.Payload as ToolResultPayload;
        var content = payload?.Success == true
            ? $"✅ Tool execution complete"
            : $"❌ Tool execution failed";

        return new StreamingChatEvent
        {
            Type = "thinking",
            Content = content,
            MessageId = @event.Id,
            Timestamp = @event.Timestamp.UtcDateTime,
            Metadata = new Dictionary<string, object>
            {
                ["eventType"] = "tool.result",
                ["callId"] = payload?.CallId,
                ["success"] = payload?.Success ?? false,
                ["durationMs"] = payload?.DurationMs ?? 0,
                ["correlationId"] = @event.CorrelationId
            }
        };
    }

    private static StreamingChatEvent TransformPhaseLifecycle(StreamingEvent @event)
    {
        var payload = @event.Payload as PhaseLifecyclePayload;
        var statusEmoji = payload?.Status == "started" ? "▶️" : "⏹️";
        var content = $"{statusEmoji} Phase {payload?.Phase}: {payload?.Status}";

        return new StreamingChatEvent
        {
            Type = "thinking",
            Content = content,
            MessageId = @event.Id,
            Timestamp = @event.Timestamp.UtcDateTime,
            Metadata = new Dictionary<string, object>
            {
                ["eventType"] = "phase.lifecycle",
                ["phase"] = payload?.Phase,
                ["status"] = payload?.Status,
                ["correlationId"] = @event.CorrelationId
            }
        };
    }

    private static StreamingChatEvent TransformRunLifecycle(StreamingEvent @event)
    {
        var payload = @event.Payload as RunLifecyclePayload;
        var statusEmoji = payload?.Status == "started" ? "🚀" : "🏁";
        var content = payload?.Status == "started"
            ? $"{statusEmoji} Run started"
            : $"{statusEmoji} Run {(payload?.Success == true ? "completed" : "failed")}";

        if (payload?.Status == "finished" && payload?.DurationMs > 0)
        {
            content += $" ({payload.DurationMs}ms)";
        }

        return new StreamingChatEvent
        {
            Type = "thinking",
            Content = content,
            MessageId = @event.Id,
            Timestamp = @event.Timestamp.UtcDateTime,
            Metadata = new Dictionary<string, object>
            {
                ["eventType"] = "run.lifecycle",
                ["status"] = payload?.Status,
                ["runId"] = payload?.RunId,
                ["success"] = payload?.Success,
                ["correlationId"] = @event.CorrelationId
            }
        };
    }

    private static StreamingChatEvent TransformError(StreamingEvent @event)
    {
        var payload = @event.Payload as ErrorPayload;

        return new StreamingChatEvent
        {
            Type = "error",
            Content = payload?.Message ?? "An error occurred",
            MessageId = @event.Id,
            Timestamp = @event.Timestamp.UtcDateTime,
            Metadata = new Dictionary<string, object>
            {
                ["eventType"] = "error",
                ["code"] = payload?.Code,
                ["severity"] = payload?.Severity ?? "error",
                ["recoverable"] = payload?.Recoverable ?? false,
                ["correlationId"] = @event.CorrelationId
            }
        };
    }

    private static StreamingChatEvent TransformDefault(StreamingEvent @event)
    {
        return new StreamingChatEvent
        {
            Type = "content_chunk",
            Content = string.Empty,
            MessageId = @event.Id,
            Timestamp = @event.Timestamp.UtcDateTime,
            Metadata = new Dictionary<string, object>
            {
                ["eventType"] = @event.Type.ToString(),
                ["correlationId"] = @event.CorrelationId
            }
        };
    }
}
