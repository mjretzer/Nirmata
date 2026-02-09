namespace Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;

/// <summary>
/// Represents a role in an LLM conversation.
/// </summary>
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
}
