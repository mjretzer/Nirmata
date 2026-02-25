namespace Gmsd.Agents.Execution.Execution.AtomicGitCommitter;

/// <summary>
/// Represents the result of an atomic Git commit operation.
/// </summary>
public sealed class CommitResult
{
    /// <summary>
    /// Whether the commit operation succeeded.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// The Git commit hash if the commit succeeded; otherwise null.
    /// </summary>
    public string? CommitHash { get; init; }

    /// <summary>
    /// Statistics about the diff (files changed, insertions, deletions).
    /// </summary>
    public DiffStat? DiffStat { get; init; }

    /// <summary>
    /// Error message if the commit failed; otherwise null.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The list of files that were actually staged and committed.
    /// </summary>
    public IReadOnlyList<string> FilesStaged { get; init; } = Array.Empty<string>();
}
