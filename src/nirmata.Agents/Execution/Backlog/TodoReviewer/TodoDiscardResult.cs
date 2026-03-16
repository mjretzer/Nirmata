namespace nirmata.Agents.Execution.Backlog.TodoReviewer;

/// <summary>
/// Represents a request to discard a TODO.
/// </summary>
public sealed record TodoDiscardRequest
{
    public required string WorkspaceRoot { get; init; }
    public required string TodoId { get; init; }
    public string? Rationale { get; init; }
    public bool Archive { get; init; } = true;
    public bool WriteEvent { get; init; } = true;
}

/// <summary>
/// Represents the result of discarding a TODO.
/// </summary>
public sealed record TodoDiscardResult
{
    public required bool IsSuccess { get; init; }
    public string? TodoId { get; init; }
    public string? ArchivePath { get; init; }
    public bool EventWritten { get; init; }
    public string? ErrorMessage { get; init; }
}
