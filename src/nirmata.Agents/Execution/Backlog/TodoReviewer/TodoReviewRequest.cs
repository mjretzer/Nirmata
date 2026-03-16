namespace nirmata.Agents.Execution.Backlog.TodoReviewer;

/// <summary>
/// Represents a request to list TODOs for review.
/// </summary>
public sealed record TodoReviewRequest
{
    /// <summary>
    /// The root path of the workspace containing the .aos directory.
    /// </summary>
    public required string WorkspaceRoot { get; init; }

    /// <summary>
    /// Optional filter by status. If null, returns TODOs with status "captured" or "reviewing".
    /// </summary>
    public string? StatusFilter { get; init; }

    /// <summary>
    /// Optional filter by priority.
    /// </summary>
    public string? PriorityFilter { get; init; }
}
