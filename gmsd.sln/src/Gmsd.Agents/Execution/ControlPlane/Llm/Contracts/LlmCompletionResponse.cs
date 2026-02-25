namespace Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;

/// <summary>
/// Normalized response from an LLM completion.
/// </summary>
[Obsolete("Use Microsoft.SemanticKernel.ChatCompletion.ChatMessageContent directly. " +
          "This abstraction will be removed in a future release.", false)]
public record LlmCompletionResponse
{
    /// <summary>
    /// The generated message from the assistant.
    /// </summary>
    public required LlmMessage Message { get; init; }

    /// <summary>
    /// The model identifier that generated the response.
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// The provider that generated the response.
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Token usage information if available.
    /// </summary>
    public LlmTokenUsage? Usage { get; init; }

    /// <summary>
    /// The reason the completion finished (e.g., "stop", "tool_calls", "length").
    /// </summary>
    public string? FinishReason { get; init; }

    /// <summary>
    /// Tool calls requested by the LLM if tool calling was enabled.
    /// </summary>
    public IReadOnlyList<LlmToolCall>? ToolCalls { get; init; }

    /// <summary>
    /// Response timestamp in UTC.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Indicates whether the LLM has requested tool calls in this response.
    /// This is true when FinishReason is "tool_calls" or when ToolCalls is non-empty.
    /// </summary>
    public bool HasToolCalls =>
        FinishReason == "tool_calls" ||
        (ToolCalls?.Count > 0);

    /// <summary>
    /// Indicates whether this response represents a completed conversation turn
    /// without requiring further tool execution.
    /// </summary>
    public bool IsComplete =>
        !HasToolCalls &&
        (FinishReason == "stop" || FinishReason == "end_turn");

    /// <summary>
    /// Creates a tool result message for the specified tool call.
    /// </summary>
    /// <param name="toolCallId">The ID of the tool call this result is for.</param>
    /// <param name="toolName">The name of the tool that was invoked.</param>
    /// <param name="result">The result content, typically JSON.</param>
    /// <returns>A properly formatted tool result message.</returns>
    public static LlmMessage CreateToolResultMessage(string toolCallId, string toolName, string result) =>
        LlmMessage.Tool(toolCallId, toolName, result);

    /// <summary>
    /// Creates an error tool result message for a failed tool call.
    /// </summary>
    /// <param name="toolCallId">The ID of the tool call this result is for.</param>
    /// <param name="toolName">The name of the tool that failed.</param>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <returns>A properly formatted error tool result message.</returns>
    public static LlmMessage CreateToolErrorMessage(string toolCallId, string toolName, string errorMessage)
    {
        var errorJson = global::System.Text.Json.JsonSerializer.Serialize(new { error = errorMessage });
        return LlmMessage.Tool(toolCallId, toolName, errorJson);
    }
}

/// <summary>
/// Token usage statistics for an LLM completion.
/// </summary>
[Obsolete("This abstraction will be removed in a future release.", false)]
public sealed record LlmTokenUsage
{
    /// <summary>
    /// Tokens consumed in the prompt.
    /// </summary>
    public int PromptTokens { get; init; }

    /// <summary>
    /// Tokens generated in the completion.
    /// </summary>
    public int CompletionTokens { get; init; }

    /// <summary>
    /// Total tokens consumed (prompt + completion).
    /// </summary>
    public int TotalTokens => PromptTokens + CompletionTokens;
}
