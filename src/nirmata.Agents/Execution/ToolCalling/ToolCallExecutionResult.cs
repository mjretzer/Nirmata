namespace nirmata.Agents.Execution.ToolCalling;

/// <summary>
/// Represents the result of executing a single tool call within the tool calling loop.
/// Correlates the tool call to its execution result or error.
/// </summary>
public sealed record ToolCallExecutionResult
{
    /// <summary>
    /// The unique identifier of the tool call this result corresponds to.
    /// </summary>
    public required string ToolCallId { get; init; }

    /// <summary>
    /// The name of the tool that was invoked.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// The arguments that were passed to the tool, as a JSON string.
    /// </summary>
    public required string ArgumentsJson { get; init; }

    /// <summary>
    /// Indicates whether the tool execution succeeded.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// The result content as a string (typically JSON) when execution succeeds.
    /// </summary>
    public string? ResultContent { get; init; }

    /// <summary>
    /// Error information when execution fails.
    /// </summary>
    public ToolCallExecutionError? Error { get; init; }

    /// <summary>
    /// Timestamp when the tool execution started.
    /// </summary>
    public DateTime StartedAt { get; init; }

    /// <summary>
    /// Timestamp when the tool execution completed.
    /// </summary>
    public DateTime CompletedAt { get; init; }

    /// <summary>
    /// Duration of the tool execution.
    /// </summary>
    public TimeSpan Duration => CompletedAt - StartedAt;

    /// <summary>
    /// Additional metadata about the execution.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Creates a successful execution result.
    /// </summary>
    public static ToolCallExecutionResult Success(
        string toolCallId,
        string toolName,
        string argumentsJson,
        string resultContent,
        DateTime startedAt,
        IReadOnlyDictionary<string, string>? metadata = null) => new()
    {
        ToolCallId = toolCallId,
        ToolName = toolName,
        ArgumentsJson = argumentsJson,
        IsSuccess = true,
        ResultContent = resultContent,
        StartedAt = startedAt,
        CompletedAt = DateTime.UtcNow,
        Metadata = metadata ?? new Dictionary<string, string>()
    };

    /// <summary>
    /// Creates a failed execution result.
    /// </summary>
    public static ToolCallExecutionResult Failure(
        string toolCallId,
        string toolName,
        string argumentsJson,
        string errorCode,
        string errorMessage,
        DateTime startedAt,
        string? exceptionDetails = null,
        IReadOnlyDictionary<string, string>? metadata = null) => new()
    {
        ToolCallId = toolCallId,
        ToolName = toolName,
        ArgumentsJson = argumentsJson,
        IsSuccess = false,
        Error = new ToolCallExecutionError(errorCode, errorMessage, exceptionDetails),
        StartedAt = startedAt,
        CompletedAt = DateTime.UtcNow,
        Metadata = metadata ?? new Dictionary<string, string>()
    };
}

/// <summary>
/// Error information from a failed tool call execution.
/// </summary>
public sealed record ToolCallExecutionError
{
    /// <summary>
    /// Error code identifying the type of error.
    /// </summary>
    public string Code { get; init; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public string Message { get; init; }

    /// <summary>
    /// Optional exception details for debugging.
    /// </summary>
    public string? ExceptionDetails { get; init; }

    public ToolCallExecutionError(string code, string message, string? exceptionDetails = null)
    {
        Code = code;
        Message = message;
        ExceptionDetails = exceptionDetails;
    }
}
