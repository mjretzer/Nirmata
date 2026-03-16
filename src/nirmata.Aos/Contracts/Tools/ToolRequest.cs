namespace nirmata.Aos.Contracts.Tools;

/// <summary>
/// Standardized request shape for tool invocations.
/// Contains the input parameters and metadata for a single tool call.
/// </summary>
public sealed class ToolRequest
{
    /// <summary>
    /// The name of the operation/action to invoke on the tool.
    /// </summary>
    public required string Operation { get; init; }

    /// <summary>
    /// The input parameters for the tool invocation, keyed by parameter name.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    /// Optional identifier for the request, used for correlation and deduplication.
    /// </summary>
    public string? RequestId { get; init; }

    /// <summary>
    /// Optional metadata for the request, such as routing hints or priority.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
