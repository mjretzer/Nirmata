namespace Gmsd.Agents.Execution.ToolCalling;

/// <summary>
/// Event raised when the LLM requests tool calls.
/// </summary>
public sealed class ToolCallDetectedEvent : ToolCallingEvent
{
    public ToolCallDetectedEvent()
    {
        EventType = ToolCallingEventType.ToolCallDetected;
    }

    /// <summary>
    /// The iteration number in the loop (1-indexed).
    /// </summary>
    public required int Iteration { get; init; }

    /// <summary>
    /// The tool calls requested by the LLM.
    /// </summary>
    public required IReadOnlyList<ToolCallDetectedInfo> ToolCalls { get; init; }
}

/// <summary>
/// Information about a detected tool call.
/// </summary>
public sealed record ToolCallDetectedInfo
{
    /// <summary>
    /// Unique identifier for this tool call.
    /// </summary>
    public required string ToolCallId { get; init; }

    /// <summary>
    /// Name of the tool being called.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Arguments as a JSON string.
    /// </summary>
    public required string ArgumentsJson { get; init; }
}

/// <summary>
/// Event raised when an individual tool execution begins.
/// </summary>
public sealed class ToolCallStartedEvent : ToolCallingEvent
{
    public ToolCallStartedEvent()
    {
        EventType = ToolCallingEventType.ToolCallStarted;
    }

    /// <summary>
    /// The iteration number in the loop.
    /// </summary>
    public required int Iteration { get; init; }

    /// <summary>
    /// Unique identifier for this tool call.
    /// </summary>
    public required string ToolCallId { get; init; }

    /// <summary>
    /// Name of the tool being executed.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Arguments being passed to the tool.
    /// </summary>
    public required string ArgumentsJson { get; init; }
}

/// <summary>
/// Event raised when a tool execution succeeds.
/// </summary>
public sealed class ToolCallCompletedEvent : ToolCallingEvent
{
    public ToolCallCompletedEvent()
    {
        EventType = ToolCallingEventType.ToolCallCompleted;
    }

    /// <summary>
    /// The iteration number in the loop.
    /// </summary>
    public required int Iteration { get; init; }

    /// <summary>
    /// Unique identifier for this tool call.
    /// </summary>
    public required string ToolCallId { get; init; }

    /// <summary>
    /// Name of the tool that was executed.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Duration of the tool execution.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Indicates if there is result content (may be truncated for large results).
    /// </summary>
    public bool HasResult { get; init; }

    /// <summary>
    /// Summary or preview of the result (truncated if large).
    /// </summary>
    public string? ResultSummary { get; init; }
}

/// <summary>
/// Event raised when a tool execution fails.
/// </summary>
public sealed class ToolCallFailedEvent : ToolCallingEvent
{
    public ToolCallFailedEvent()
    {
        EventType = ToolCallingEventType.ToolCallFailed;
    }

    /// <summary>
    /// The iteration number in the loop.
    /// </summary>
    public required int Iteration { get; init; }

    /// <summary>
    /// Unique identifier for this tool call.
    /// </summary>
    public required string ToolCallId { get; init; }

    /// <summary>
    /// Name of the tool that failed.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Error code.
    /// </summary>
    public required string ErrorCode { get; init; }

    /// <summary>
    /// Error message.
    /// </summary>
    public required string ErrorMessage { get; init; }

    /// <summary>
    /// Duration of the tool execution before failure.
    /// </summary>
    public required TimeSpan Duration { get; init; }
}

/// <summary>
/// Event raised when tool results are sent back to the LLM.
/// </summary>
public sealed class ToolResultsSubmittedEvent : ToolCallingEvent
{
    public ToolResultsSubmittedEvent()
    {
        EventType = ToolCallingEventType.ToolResultsSubmitted;
    }

    /// <summary>
    /// The iteration number in the loop.
    /// </summary>
    public required int Iteration { get; init; }

    /// <summary>
    /// Number of tool results being submitted.
    /// </summary>
    public required int ResultCount { get; init; }

    /// <summary>
    /// Information about the submitted results.
    /// </summary>
    public required IReadOnlyList<ToolResultSubmittedInfo> Results { get; init; }
}

/// <summary>
/// Information about a submitted tool result.
/// </summary>
public sealed record ToolResultSubmittedInfo
{
    /// <summary>
    /// Unique identifier for this tool call.
    /// </summary>
    public required string ToolCallId { get; init; }

    /// <summary>
    /// Name of the tool.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Whether the tool execution succeeded.
    /// </summary>
    public required bool IsSuccess { get; init; }
}

/// <summary>
/// Event raised when one full iteration (LLM call + tool executions) completes.
/// </summary>
public sealed class ToolLoopIterationCompletedEvent : ToolCallingEvent
{
    public ToolLoopIterationCompletedEvent()
    {
        EventType = ToolCallingEventType.LoopIterationCompleted;
    }

    /// <summary>
    /// The iteration number that completed.
    /// </summary>
    public required int Iteration { get; init; }

    /// <summary>
    /// Whether the LLM requested more tool calls in this iteration.
    /// </summary>
    public required bool HasMoreToolCalls { get; init; }

    /// <summary>
    /// Number of tool calls executed in this iteration.
    /// </summary>
    public required int ToolCallCount { get; init; }

    /// <summary>
    /// Duration of the iteration.
    /// </summary>
    public required TimeSpan Duration { get; init; }
}

/// <summary>
/// Event raised when the tool calling loop finishes normally.
/// </summary>
public sealed class ToolLoopCompletedEvent : ToolCallingEvent
{
    public ToolLoopCompletedEvent()
    {
        EventType = ToolCallingEventType.LoopCompleted;
    }

    /// <summary>
    /// Total number of iterations performed.
    /// </summary>
    public required int TotalIterations { get; init; }

    /// <summary>
    /// Total number of tool calls executed across all iterations.
    /// </summary>
    public required int TotalToolCalls { get; init; }

    /// <summary>
    /// The reason the loop completed.
    /// </summary>
    public required ToolCallingCompletionReason CompletionReason { get; init; }

    /// <summary>
    /// Total duration of the loop.
    /// </summary>
    public required TimeSpan TotalDuration { get; init; }
}

/// <summary>
/// Event raised when the tool calling loop encounters an error.
/// </summary>
public sealed class ToolLoopFailedEvent : ToolCallingEvent
{
    public ToolLoopFailedEvent()
    {
        EventType = ToolCallingEventType.LoopFailed;
    }

    /// <summary>
    /// Error code.
    /// </summary>
    public required string ErrorCode { get; init; }

    /// <summary>
    /// Error message.
    /// </summary>
    public required string ErrorMessage { get; init; }

    /// <summary>
    /// The iteration when the error occurred.
    /// </summary>
    public required int Iteration { get; init; }
}

/// <summary>
/// Base class for all tool calling events.
/// </summary>
public abstract class ToolCallingEvent
{
    /// <summary>
    /// Unique identifier for this event.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The type of tool calling event.
    /// </summary>
    public ToolCallingEventType EventType { get; init; }

    /// <summary>
    /// ISO 8601 timestamp when the event was emitted.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optional correlation ID for tracing events across a conversation.
    /// </summary>
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Tool calling event types.
/// </summary>
public enum ToolCallingEventType
{
    /// <summary>
    /// LLM requested tool calls.
    /// </summary>
    ToolCallDetected,

    /// <summary>
    /// Individual tool execution began.
    /// </summary>
    ToolCallStarted,

    /// <summary>
    /// Tool execution succeeded.
    /// </summary>
    ToolCallCompleted,

    /// <summary>
    /// Tool execution failed.
    /// </summary>
    ToolCallFailed,

    /// <summary>
    /// Tool results sent back to LLM.
    /// </summary>
    ToolResultsSubmitted,

    /// <summary>
    /// One full iteration completed.
    /// </summary>
    LoopIterationCompleted,

    /// <summary>
    /// Loop finished normally.
    /// </summary>
    LoopCompleted,

    /// <summary>
    /// Loop encountered an error.
    /// </summary>
    LoopFailed
}
