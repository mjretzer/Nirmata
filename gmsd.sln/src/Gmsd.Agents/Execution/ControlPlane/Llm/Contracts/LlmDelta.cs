namespace Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;

/// <summary>
/// Represents a streaming delta chunk from an LLM completion.
/// Used in IAsyncEnumerable streaming responses.
/// </summary>
[Obsolete("Use Microsoft.SemanticKernel.ChatCompletion.StreamingChatMessageContent directly. " +
          "This abstraction will be removed in a future release.", false)]
public sealed record LlmDelta
{
    /// <summary>
    /// The content chunk from the LLM (may be partial).
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Optional finish reason if this chunk completes the response.
    /// </summary>
    public string? FinishReason { get; init; }

    /// <summary>
    /// Token usage statistics (may be null until final chunk).
    /// </summary>
    public LlmTokenUsage? Usage { get; init; }

    /// <summary>
    /// Streaming tool call information. Some providers stream tool calls
    /// incrementally with partial function names or arguments.
    /// </summary>
    public LlmStreamingToolCall? ToolCall { get; init; }

    /// <summary>
    /// Indicates if this is the final delta in the stream.
    /// </summary>
    public bool IsFinal => FinishReason != null;

    /// <summary>
    /// Indicates whether this delta contains streaming tool call information.
    /// </summary>
    public bool HasStreamingToolCall => ToolCall != null;
}

/// <summary>
/// Represents a streaming tool call chunk from an LLM.
/// Some providers stream tool calls incrementally with partial data.
/// </summary>
[Obsolete("Use Microsoft.SemanticKernel.FunctionCallContent directly. " +
          "This abstraction will be removed in a future release.", false)]
public sealed record LlmStreamingToolCall
{
    /// <summary>
    /// The ID of the tool call. May be partial or complete depending on the provider.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// The name of the tool/function being called. May be partial for some providers.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Partial arguments JSON. Some providers stream arguments as they are generated.
    /// This may be incomplete JSON that needs to be accumulated.
    /// </summary>
    public string? ArgumentsJson { get; init; }

    /// <summary>
    /// The index of this tool call in the list of tool calls.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// Indicates whether this is the final chunk for this tool call.
    /// When true, the arguments should be complete JSON.
    /// </summary>
    public bool IsComplete { get; init; }
}
