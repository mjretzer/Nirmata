namespace nirmata.Agents.Execution.Backlog.TodoCapturer;

/// <summary>
/// Represents the result of a TODO capture operation.
/// </summary>
public sealed record TodoCaptureResult
{
    /// <summary>
    /// Indicates whether the capture succeeded.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// The generated TODO ID (e.g., "TODO-001").
    /// </summary>
    public string? TodoId { get; init; }

    /// <summary>
    /// The file path where the TODO was written.
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Indicates whether the capture event was successfully written to events.ndjson.
    /// </summary>
    public bool EventWritten { get; init; }

    /// <summary>
    /// Error message if the capture failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
