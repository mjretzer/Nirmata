namespace nirmata.Agents.Execution.ControlPlane.Llm.Contracts;

/// <summary>
/// Represents a role in an LLM conversation.
/// </summary>
[Obsolete("Use Microsoft.SemanticKernel.ChatCompletion.AuthorRole directly. " +
          "This abstraction will be removed in a future release.", false)]
public enum LlmMessageRole
{
    /// <summary>
    /// System-level instructions or context.
    /// </summary>
    System,

    /// <summary>
    /// User input or query.
    /// </summary>
    User,

    /// <summary>
    /// Assistant response, potentially including tool calls.
    /// </summary>
    Assistant,

    /// <summary>
    /// Tool result returned to the assistant.
    /// </summary>
    Tool
}

/// <summary>
/// Normalized representation of a message in an LLM conversation.
/// </summary>
[Obsolete("Use Microsoft.SemanticKernel.ChatCompletion.ChatMessageContent directly. " +
          "This abstraction will be removed in a future release.", false)]
public sealed record LlmMessage
{
    /// <summary>
    /// The role of the message sender.
    /// </summary>
    public required LlmMessageRole Role { get; init; }

    /// <summary>
    /// The text content of the message. May be null when the message contains only tool calls.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Tool calls requested by the assistant. Only applicable when Role is Assistant.
    /// </summary>
    public IReadOnlyList<LlmToolCall>? ToolCalls { get; init; }

    /// <summary>
    /// The ID of the tool call this message is responding to. Only applicable when Role is Tool.
    /// </summary>
    public string? ToolCallId { get; init; }

    /// <summary>
    /// The name of the tool being called. Only applicable when Role is Tool.
    /// </summary>
    public string? ToolName { get; init; }

    /// <summary>
    /// Indicates whether this message contains tool calls (for assistant messages).
    /// </summary>
    public bool HasToolCalls => ToolCalls?.Count > 0;

    /// <summary>
    /// Indicates whether this is a tool result message.
    /// </summary>
    public bool IsToolResult => Role == LlmMessageRole.Tool;

    /// <summary>
    /// Creates a system message.
    /// </summary>
    public static LlmMessage System(string content) =>
        new() { Role = LlmMessageRole.System, Content = content };

    /// <summary>
    /// Creates a user message.
    /// </summary>
    public static LlmMessage User(string content) =>
        new() { Role = LlmMessageRole.User, Content = content };

    /// <summary>
    /// Creates an assistant message.
    /// </summary>
    public static LlmMessage Assistant(string? content = null, IReadOnlyList<LlmToolCall>? toolCalls = null) =>
        new() { Role = LlmMessageRole.Assistant, Content = content, ToolCalls = toolCalls };

    /// <summary>
    /// Creates a tool result message.
    /// </summary>
    public static LlmMessage Tool(string toolCallId, string toolName, string content) =>
        new() { Role = LlmMessageRole.Tool, ToolCallId = toolCallId, ToolName = toolName, Content = content };

    /// <summary>
    /// Creates a tool result message from a successful tool execution.
    /// </summary>
    /// <param name="toolCallId">The ID of the tool call this result is for.</param>
    /// <param name="toolName">The name of the tool that was invoked.</param>
    /// <param name="result">The result object, which will be serialized to JSON.</param>
    /// <returns>A properly formatted tool result message.</returns>
    public static LlmMessage ToolResult<T>(string toolCallId, string toolName, T result)
    {
        var content = result is string s ? s : global::System.Text.Json.JsonSerializer.Serialize(result);
        return Tool(toolCallId, toolName, content);
    }

    /// <summary>
    /// Creates a tool result message from a failed tool execution.
    /// </summary>
    /// <param name="toolCallId">The ID of the tool call this error is for.</param>
    /// <param name="toolName">The name of the tool that failed.</param>
    /// <param name="errorCode">The error code identifying the type of failure.</param>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <returns>A properly formatted tool error message.</returns>
    public static LlmMessage ToolError(string toolCallId, string toolName, string errorCode, string errorMessage)
    {
        var errorContent = global::System.Text.Json.JsonSerializer.Serialize(new
        {
            error = errorMessage,
            code = errorCode
        });
        return Tool(toolCallId, toolName, errorContent);
    }

    /// <summary>
    /// Creates a tool result message from an exception.
    /// </summary>
    /// <param name="toolCallId">The ID of the tool call this error is for.</param>
    /// <param name="toolName">The name of the tool that failed.</param>
    /// <param name="exception">The exception that occurred during tool execution.</param>
    /// <returns>A properly formatted tool error message.</returns>
    public static LlmMessage ToolException(string toolCallId, string toolName, Exception exception)
    {
        var errorContent = global::System.Text.Json.JsonSerializer.Serialize(new
        {
            error = exception.Message,
            code = "ExecutionError",
            type = exception.GetType().Name
        });
        return Tool(toolCallId, toolName, errorContent);
    }
}
