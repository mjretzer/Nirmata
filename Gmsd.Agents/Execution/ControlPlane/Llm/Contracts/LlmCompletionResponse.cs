namespace Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;

/// <summary>
/// Normalized response from an LLM completion.
/// </summary>
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
    /// Response timestamp in UTC.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Token usage statistics for an LLM completion.
/// </summary>
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
