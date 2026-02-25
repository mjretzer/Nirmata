namespace Gmsd.Agents.Execution.ControlPlane.Chat.Models;

/// <summary>
/// Represents a chat request from the user.
/// </summary>
public sealed class ChatRequest
{
    /// <summary>
    /// The user input message.
    /// </summary>
    public required string Input { get; init; }

    /// <summary>
    /// Whether to include workspace context (specs, state, commands) in the response.
    /// </summary>
    public bool IncludeWorkspaceContext { get; init; } = true;

    /// <summary>
    /// Optional conversation history for multi-turn chat.
    /// </summary>
    public IReadOnlyList<ChatMessage> ConversationHistory { get; init; } = Array.Empty<ChatMessage>();

    /// <summary>
    /// Optional maximum tokens for the response.
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Optional temperature for response generation (0.0 - 2.0).
    /// </summary>
    public double? Temperature { get; init; }
}

/// <summary>
/// Represents a single message in the conversation history.
/// </summary>
public sealed class ChatMessage
{
    /// <summary>
    /// The role of the message sender (user, assistant, system).
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// The message content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Optional timestamp for the message.
    /// </summary>
    public DateTime? Timestamp { get; init; }
}

/// <summary>
/// Represents a chat response from the assistant.
/// </summary>
public sealed class ChatResponse
{
    /// <summary>
    /// The generated response content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// The LLM model used to generate the response.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Number of tokens in the prompt.
    /// </summary>
    public int PromptTokens { get; init; }

    /// <summary>
    /// Number of tokens in the completion.
    /// </summary>
    public int CompletionTokens { get; init; }

    /// <summary>
    /// Total tokens used (prompt + completion).
    /// </summary>
    public int TotalTokens => PromptTokens + CompletionTokens;

    /// <summary>
    /// Duration of the LLM call in milliseconds.
    /// </summary>
    public long DurationMs { get; init; }

    /// <summary>
    /// Whether the response was generated successfully.
    /// </summary>
    public bool IsSuccess { get; init; } = true;

    /// <summary>
    /// Error message if the response failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Correlation ID for tracing.
    /// </summary>
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Represents a delta (chunk) of a streaming chat response.
/// </summary>
public sealed class ChatDelta
{
    /// <summary>
    /// The content delta (chunk of text).
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Whether this is the final delta in the stream.
    /// </summary>
    public bool IsComplete { get; init; }

    /// <summary>
    /// Total tokens used so far (if available).
    /// </summary>
    public int? TotalTokens { get; init; }
}
