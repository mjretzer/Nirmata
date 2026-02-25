using System.Text.Json.Serialization;

namespace Gmsd.Agents.Execution.ToolCalling;

/// <summary>
/// Evidence envelope for a complete tool calling conversation loop.
/// Captures the full conversation history, tool executions, and metadata for auditability.
/// </summary>
public sealed record ToolCallingConversationEvidence
{
    /// <summary>
    /// Schema version for evolution compatibility.
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// Unique identifier for this tool calling conversation.
    /// </summary>
    public required string CallId { get; init; }

    /// <summary>
    /// The run ID for correlation with other evidence.
    /// </summary>
    public required string RunId { get; init; }

    /// <summary>
    /// Correlation ID for tracing across the system.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Timestamp when the conversation started.
    /// </summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// Timestamp when the conversation completed.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// The reason the conversation completed.
    /// </summary>
    public ToolCallingCompletionReason CompletionReason { get; init; }

    /// <summary>
    /// The complete conversation history including user, assistant, and tool messages.
    /// </summary>
    public required IReadOnlyList<ToolCallingConversationMessage> ConversationHistory { get; init; }

    /// <summary>
    /// Tool executions performed during the conversation.
    /// </summary>
    public IReadOnlyList<ToolCallingExecutionEvidence> ToolExecutions { get; init; } = Array.Empty<ToolCallingExecutionEvidence>();

    /// <summary>
    /// Aggregated usage statistics across all iterations.
    /// </summary>
    public ToolCallingUsageEvidence? Usage { get; init; }

    /// <summary>
    /// Options used for this conversation.
    /// </summary>
    public ToolCallingOptionsEvidence? Options { get; init; }

    /// <summary>
    /// Error information if the conversation failed.
    /// </summary>
    public ToolCallingErrorEvidence? Error { get; init; }

    /// <summary>
    /// Additional metadata about the conversation.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// A message in the tool calling conversation history.
/// </summary>
public sealed record ToolCallingConversationMessage
{
    /// <summary>
    /// The role of the message sender.
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// The content of the message.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Tool calls requested in this message (for assistant messages).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ToolCallingRequestEvidence>? ToolCalls { get; init; }

    /// <summary>
    /// The ID of the tool call this message is responding to (for tool messages).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolCallId { get; init; }

    /// <summary>
    /// The name of the tool being called (for tool messages).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolName { get; init; }

    /// <summary>
    /// Timestamp when the message was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Evidence of a tool call request.
/// </summary>
public sealed record ToolCallingRequestEvidence
{
    /// <summary>
    /// Unique identifier for this tool call.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Name of the tool being called.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Arguments as a JSON string.
    /// </summary>
    public required string ArgumentsJson { get; init; }
}

/// <summary>
/// Evidence of a tool execution.
/// </summary>
public sealed record ToolCallingExecutionEvidence
{
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
    /// Arguments passed to the tool.
    /// </summary>
    public required string ArgumentsJson { get; init; }

    /// <summary>
    /// Whether the execution succeeded.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// Result content if successful.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResultContent { get; init; }

    /// <summary>
    /// Error information if failed.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ToolCallingErrorEvidence? Error { get; init; }

    /// <summary>
    /// Timestamp when execution started.
    /// </summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// Timestamp when execution completed.
    /// </summary>
    public DateTimeOffset CompletedAt { get; init; }

    /// <summary>
    /// Duration of the execution.
    /// </summary>
    public TimeSpan Duration => CompletedAt - StartedAt;
}

/// <summary>
/// Usage statistics for the conversation.
/// </summary>
public sealed record ToolCallingUsageEvidence
{
    /// <summary>
    /// Total tokens in all prompts.
    /// </summary>
    public int TotalPromptTokens { get; init; }

    /// <summary>
    /// Total tokens in all completions.
    /// </summary>
    public int TotalCompletionTokens { get; init; }

    /// <summary>
    /// Total tokens consumed.
    /// </summary>
    public int TotalTokens => TotalPromptTokens + TotalCompletionTokens;

    /// <summary>
    /// Number of iterations performed.
    /// </summary>
    public int IterationCount { get; init; }

    /// <summary>
    /// Total number of tool calls executed.
    /// </summary>
    public int TotalToolCalls { get; init; }
}

/// <summary>
/// Options used for the conversation.
/// </summary>
public sealed record ToolCallingOptionsEvidence
{
    /// <summary>
    /// Maximum iterations allowed.
    /// </summary>
    public int MaxIterations { get; init; }

    /// <summary>
    /// Timeout duration.
    /// </summary>
    public TimeSpan Timeout { get; init; }

    /// <summary>
    /// Model used.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Whether parallel tool execution was enabled.
    /// </summary>
    public bool EnableParallelToolExecution { get; init; }
}

/// <summary>
/// Error information for evidence.
/// </summary>
public sealed record ToolCallingErrorEvidence
{
    /// <summary>
    /// Error code.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Exception details for debugging.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExceptionDetails { get; init; }
}
