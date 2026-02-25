namespace Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;

/// <summary>
/// Normalized representation of a tool call requested by an LLM.
/// </summary>
[Obsolete("Use Microsoft.SemanticKernel.FunctionCallContent directly. " +
          "This abstraction will be removed in a future release.", false)]
public sealed record LlmToolCall
{
    /// <summary>
    /// Unique identifier for this tool call.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Name of the tool/function to invoke.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Arguments as a JSON string. Providers may return this as a string or parsed object.
    /// </summary>
    public required string ArgumentsJson { get; init; }
}

/// <summary>
/// Normalized representation of a tool result to return to the LLM.
/// </summary>
[Obsolete("Use Microsoft.SemanticKernel.FunctionResultContent directly. " +
          "This abstraction will be removed in a future release.", false)]
public sealed record LlmToolResult
{
    /// <summary>
    /// The ID of the tool call this result corresponds to.
    /// </summary>
    public required string ToolCallId { get; init; }

    /// <summary>
    /// Name of the tool that was invoked.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Result content as a string, typically JSON.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Indicates whether the tool invocation resulted in an error.
    /// </summary>
    public bool IsError { get; init; }
}
