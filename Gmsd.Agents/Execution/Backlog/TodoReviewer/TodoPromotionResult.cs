namespace Gmsd.Agents.Execution.Backlog.TodoReviewer;

/// <summary>
/// Represents a request to promote a TODO.
/// </summary>
public sealed record TodoPromotionRequest
{
    public required string WorkspaceRoot { get; init; }
    public required string TodoId { get; init; }
    public string? Title { get; init; }
    public string? AdditionalDescription { get; init; }
    public bool WriteEvent { get; init; } = true;
}

/// <summary>
/// Represents the result of promoting a TODO.
/// </summary>
public sealed record TodoPromotionResult
{
    public required bool IsSuccess { get; init; }
    public string? TodoId { get; init; }
    public string? CreatedId { get; init; }
    public string? CreatedType { get; init; }
    public string? CreatedFilePath { get; init; }
    public bool EventWritten { get; init; }
    public string? ErrorMessage { get; init; }
}
