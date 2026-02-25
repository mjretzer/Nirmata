namespace Gmsd.Agents.Execution.ToolCalling;

/// <summary>
/// Request model for initiating a tool calling conversation loop.
/// Contains all necessary inputs for the multi-step protocol.
/// </summary>
public sealed record ToolCallingRequest
{
    /// <summary>
    /// The initial conversation messages to send to the LLM.
    /// This typically includes system instructions and user queries.
    /// </summary>
    public required IReadOnlyList<ToolCallingMessage> Messages { get; init; }

    /// <summary>
    /// Tool definitions available for this conversation.
    /// The LLM may request execution of any of these tools.
    /// </summary>
    public IReadOnlyList<ToolCallingToolDefinition> Tools { get; init; } = Array.Empty<ToolCallingToolDefinition>();

    /// <summary>
    /// Options for controlling the tool calling loop behavior.
    /// </summary>
    public ToolCallingOptions Options { get; init; } = new();

    /// <summary>
    /// Correlation identifier for tracing and observability.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Additional context data passed through to tool executions and events.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Context { get; init; } = new Dictionary<string, object?>();
}

/// <summary>
/// Represents a message in a tool calling conversation.
/// Simplified model optimized for the tool calling protocol.
/// </summary>
public sealed record ToolCallingMessage
{
    /// <summary>
    /// The role of the message sender.
    /// </summary>
    public required ToolCallingRole Role { get; init; }

    /// <summary>
    /// The text content of the message. May be null when the message contains only tool calls.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Tool calls requested by the assistant. Only applicable when Role is Assistant.
    /// </summary>
    public IReadOnlyList<ToolCallingRequestItem>? ToolCalls { get; init; }

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
    public static ToolCallingMessage System(string content) =>
        new() { Role = ToolCallingRole.System, Content = content };

    /// <summary>
    /// Creates a user message.
    /// </summary>
    public static ToolCallingMessage User(string content) =>
        new() { Role = ToolCallingRole.User, Content = content };

    /// <summary>
    /// Creates an assistant message.
    /// </summary>
    public static ToolCallingMessage Assistant(string? content = null, IReadOnlyList<ToolCallingRequestItem>? toolCalls = null) =>
        new() { Role = ToolCallingRole.Assistant, Content = content, ToolCalls = toolCalls };

    /// <summary>
    /// Creates a tool result message.
    /// </summary>
    public static ToolCallingMessage Tool(string toolCallId, string toolName, string content) =>
        new() { Role = ToolCallingRole.Tool, ToolCallId = toolCallId, ToolName = toolName, Content = content };
}

/// <summary>
/// Represents a role in a tool calling conversation.
/// </summary>
public enum ToolCallingRole
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
/// Represents a tool call requested by an LLM during tool calling.
/// </summary>
public sealed record ToolCallingRequestItem
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
    /// Arguments as a JSON string.
    /// </summary>
    public required string ArgumentsJson { get; init; }
}

/// <summary>
/// Definition of a tool available to the LLM during tool calling.
/// </summary>
public sealed record ToolCallingToolDefinition
{
    /// <summary>
    /// Unique name of the tool.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Description of what the tool does.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// JSON schema describing the tool's parameters.
    /// </summary>
    public required object ParametersSchema { get; init; }
}
