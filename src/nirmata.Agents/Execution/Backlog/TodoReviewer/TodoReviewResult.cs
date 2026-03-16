namespace nirmata.Agents.Execution.Backlog.TodoReviewer;

/// <summary>
/// Represents a TODO item for review.
/// </summary>
public sealed record TodoItem
{
    public required string Id { get; init; }
    public required string Description { get; init; }
    public string? Source { get; init; }
    public required string CapturedAt { get; init; }
    public required string Priority { get; init; }
    public required string Status { get; init; }
    public string? FilePath { get; init; }
}

/// <summary>
/// Represents the result of listing TODOs for review.
/// </summary>
public sealed record TodoReviewResult
{
    public required bool IsSuccess { get; init; }
    public IReadOnlyList<TodoItem> Todos { get; init; } = Array.Empty<TodoItem>();
    public int TotalCount { get; init; }
    public string? ErrorMessage { get; init; }
}
