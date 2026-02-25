using Gmsd.Agents.Execution.ToolCalling;
using Gmsd.Web.Models.Streaming;

namespace Gmsd.Web.AgentRunner;

/// <summary>
/// Adapter that converts Agents layer ToolCallingEvents to Web layer StreamingEvents.
/// Bridges the tool calling protocol events to SSE streaming format (Task 8.2).
/// </summary>
public static class ToolCallingEventAdapter
{
    /// <summary>
    /// Converts a ToolCallingEvent to a StreamingEvent
    /// </summary>
    public static StreamingEvent ToStreamingEvent(ToolCallingEvent @event)
    {
        return @event switch
        {
            ToolCallDetectedEvent detected => ConvertToolCallDetected(detected),
            ToolCallStartedEvent started => ConvertToolCallStarted(started),
            ToolCallCompletedEvent completed => ConvertToolCallCompleted(completed),
            ToolCallFailedEvent failed => ConvertToolCallFailed(failed),
            ToolResultsSubmittedEvent results => ConvertToolResultsSubmitted(results),
            ToolLoopIterationCompletedEvent iteration => ConvertToolLoopIteration(iteration),
            ToolLoopCompletedEvent completed => ConvertToolLoopCompleted(completed),
            ToolLoopFailedEvent failed => ConvertToolLoopFailed(failed),
            _ => CreateUnknownEvent(@event)
        };
    }

    private static StreamingEvent ConvertToolCallDetected(ToolCallDetectedEvent @event)
    {
        var payload = new ToolCallDetectedPayload
        {
            Iteration = @event.Iteration,
            ToolCalls = @event.ToolCalls.Select(tc => new ToolCallInfo
            {
                ToolCallId = tc.ToolCallId,
                ToolName = tc.ToolName,
                ArgumentsJson = tc.ArgumentsJson
            }).ToList()
        };

        return StreamingEvent.Create(StreamingEventType.ToolCallDetected, payload, @event.CorrelationId);
    }

    private static StreamingEvent ConvertToolCallStarted(ToolCallStartedEvent @event)
    {
        var payload = new ToolCallStartedPayload
        {
            Iteration = @event.Iteration,
            ToolCallId = @event.ToolCallId,
            ToolName = @event.ToolName,
            ArgumentsJson = @event.ArgumentsJson
        };

        return StreamingEvent.Create(StreamingEventType.ToolCallStarted, payload, @event.CorrelationId);
    }

    private static StreamingEvent ConvertToolCallCompleted(ToolCallCompletedEvent @event)
    {
        var payload = new ToolCallCompletedPayload
        {
            Iteration = @event.Iteration,
            ToolCallId = @event.ToolCallId,
            ToolName = @event.ToolName,
            DurationMs = (long)@event.Duration.TotalMilliseconds,
            HasResult = @event.HasResult,
            ResultSummary = @event.ResultSummary
        };

        return StreamingEvent.Create(StreamingEventType.ToolCallCompleted, payload, @event.CorrelationId);
    }

    private static StreamingEvent ConvertToolCallFailed(ToolCallFailedEvent @event)
    {
        var payload = new ToolCallFailedPayload
        {
            Iteration = @event.Iteration,
            ToolCallId = @event.ToolCallId,
            ToolName = @event.ToolName,
            ErrorCode = @event.ErrorCode,
            ErrorMessage = @event.ErrorMessage,
            DurationMs = (long)@event.Duration.TotalMilliseconds
        };

        return StreamingEvent.Create(StreamingEventType.ToolCallFailed, payload, @event.CorrelationId);
    }

    private static StreamingEvent ConvertToolResultsSubmitted(ToolResultsSubmittedEvent @event)
    {
        var payload = new ToolResultsSubmittedPayload
        {
            Iteration = @event.Iteration,
            ResultCount = @event.ResultCount,
            Results = @event.Results.Select(r => new ToolResultInfo
            {
                ToolCallId = r.ToolCallId,
                ToolName = r.ToolName,
                IsSuccess = r.IsSuccess
            }).ToList()
        };

        return StreamingEvent.Create(StreamingEventType.ToolResultsSubmitted, payload, @event.CorrelationId);
    }

    private static StreamingEvent ConvertToolLoopIteration(ToolLoopIterationCompletedEvent @event)
    {
        var payload = new ToolLoopIterationCompletedPayload
        {
            Iteration = @event.Iteration,
            HasMoreToolCalls = @event.HasMoreToolCalls,
            ToolCallCount = @event.ToolCallCount,
            DurationMs = (long)@event.Duration.TotalMilliseconds
        };

        return StreamingEvent.Create(StreamingEventType.ToolLoopIterationCompleted, payload, @event.CorrelationId);
    }

    private static StreamingEvent ConvertToolLoopCompleted(ToolLoopCompletedEvent @event)
    {
        var payload = new ToolLoopCompletedPayload
        {
            TotalIterations = @event.TotalIterations,
            TotalToolCalls = @event.TotalToolCalls,
            CompletionReason = @event.CompletionReason.ToString(),
            TotalDurationMs = (long)@event.TotalDuration.TotalMilliseconds
        };

        return StreamingEvent.Create(StreamingEventType.ToolLoopCompleted, payload, @event.CorrelationId);
    }

    private static StreamingEvent ConvertToolLoopFailed(ToolLoopFailedEvent @event)
    {
        var payload = new ToolLoopFailedPayload
        {
            ErrorCode = @event.ErrorCode,
            ErrorMessage = @event.ErrorMessage,
            Iteration = @event.Iteration
        };

        return StreamingEvent.Create(StreamingEventType.ToolLoopFailed, payload, @event.CorrelationId);
    }

    private static StreamingEvent CreateUnknownEvent(ToolCallingEvent @event)
    {
        var payload = new ErrorPayload
        {
            Severity = "warning",
            Code = "UnknownToolEvent",
            Message = $"Unknown tool calling event type: {@event.EventType}",
            Context = "ToolCallingEventAdapter"
        };

        return StreamingEvent.Create(StreamingEventType.Error, payload, @event.CorrelationId);
    }
}
