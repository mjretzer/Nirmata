namespace nirmata.Agents.Execution.ToolCalling;

/// <summary>
/// Result model returned from a completed tool calling conversation loop.
/// Contains the final message, complete conversation history, and usage statistics.
/// </summary>
public sealed record ToolCallingResult
{
    /// <summary>
    /// The final message from the assistant after all tool calls are resolved.
    /// </summary>
    public required ToolCallingMessage FinalMessage { get; init; }

    /// <summary>
    /// The complete conversation history including user messages, assistant responses,
    /// tool calls, and tool results across all iterations.
    /// </summary>
    public required IReadOnlyList<ToolCallingMessage> ConversationHistory { get; init; }

    /// <summary>
    /// Token usage statistics aggregated across all LLM calls in the loop.
    /// </summary>
    public ToolCallingUsageStats? Usage { get; init; }

    /// <summary>
    /// The number of iterations (LLM calls) performed in the loop.
    /// </summary>
    public int IterationCount { get; init; }

    /// <summary>
    /// The reason the loop terminated.
    /// </summary>
    public ToolCallingCompletionReason CompletionReason { get; init; }

    /// <summary>
    /// Error information if the loop terminated due to an error.
    /// </summary>
    public ToolCallingError? Error { get; init; }

    /// <summary>
    /// Metadata about the tool calling execution.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Token usage statistics for a tool calling conversation loop.
/// </summary>
public sealed record ToolCallingUsageStats
{
    /// <summary>
    /// Total tokens consumed in all prompts across iterations.
    /// </summary>
    public int TotalPromptTokens { get; init; }

    /// <summary>
    /// Total tokens generated in all completions across iterations.
    /// </summary>
    public int TotalCompletionTokens { get; init; }

    /// <summary>
    /// Total tokens consumed across all iterations.
    /// </summary>
    public int TotalTokens => TotalPromptTokens + TotalCompletionTokens;

    /// <summary>
    /// Number of LLM calls made (iterations).
    /// </summary>
    public int IterationCount { get; init; }
}

/// <summary>
/// Reasons why a tool calling loop completed.
/// </summary>
public enum ToolCallingCompletionReason
{
    /// <summary>
    /// The assistant produced a final response without requesting more tool calls.
    /// </summary>
    CompletedNaturally,

    /// <summary>
    /// The maximum number of iterations was reached.
    /// </summary>
    MaxIterationsReached,

    /// <summary>
    /// A timeout occurred during execution.
    /// </summary>
    Timeout,

    /// <summary>
    /// An error occurred during execution.
    /// </summary>
    Error,

    /// <summary>
    /// The loop was cancelled via cancellation token.
    /// </summary>
    Cancelled
}

/// <summary>
/// Error information from a failed tool calling loop.
/// </summary>
public sealed record ToolCallingError
{
    /// <summary>
    /// Error code identifying the type of error.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Optional exception details for debugging.
    /// </summary>
    public string? ExceptionDetails { get; init; }
}
