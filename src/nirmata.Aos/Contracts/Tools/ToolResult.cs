namespace nirmata.Aos.Contracts.Tools;

/// <summary>
/// Standardized result shape for tool invocations.
/// Encapsulates both success and failure states with data and error information.
/// </summary>
public sealed class ToolResult
{
    /// <summary>
    /// Indicates whether the tool invocation succeeded.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// The result data from a successful tool invocation.
    /// Null if the invocation failed.
    /// </summary>
    public object? Data { get; init; }

    /// <summary>
    /// Error information if the tool invocation failed.
    /// Null if the invocation succeeded.
    /// </summary>
    public ToolError? Error { get; init; }

    /// <summary>
    /// Optional metadata about the invocation, such as execution time or cache status.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Creates a successful tool result with the given data.
    /// </summary>
    public static ToolResult Success(object? data, IReadOnlyDictionary<string, string>? metadata = null) => new()
    {
        IsSuccess = true,
        Data = data,
        Metadata = metadata ?? new Dictionary<string, string>()
    };

    /// <summary>
    /// Creates a failed tool result with the given error information.
    /// </summary>
    public static ToolResult Failure(string errorCode, string errorMessage, IReadOnlyDictionary<string, string>? metadata = null) => new()
    {
        IsSuccess = false,
        Error = new ToolError(errorCode, errorMessage),
        Metadata = metadata ?? new Dictionary<string, string>()
    };
}

/// <summary>
/// Represents error information from a failed tool invocation.
/// </summary>
public sealed record ToolError(string Code, string Message);
