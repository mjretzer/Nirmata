namespace nirmata.Agents.Execution.Backlog.TodoCapturer;

/// <summary>
/// Represents a request to capture a TODO item.
/// </summary>
public sealed record TodoCaptureRequest
{
    /// <summary>
    /// The root path of the workspace containing the .aos directory.
    /// </summary>
    public required string WorkspaceRoot { get; init; }

    /// <summary>
    /// The TODO description (what needs to be done).
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The source context where the TODO was discovered (e.g., file path, conversation reference).
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// The priority level: low, medium, high, or urgent. Defaults to "medium".
    /// </summary>
    public string Priority { get; init; } = "medium";

    /// <summary>
    /// If true, writes capture events to .aos/state/events.ndjson.
    /// </summary>
    public bool WriteEvent { get; init; } = true;

    /// <summary>
    /// Optional explicit TODO ID. If not provided, a new ID will be generated.
    /// </summary>
    public string? TodoId { get; init; }
}
