namespace Gmsd.Agents.Execution.Execution.AtomicGitCommitter;

/// <summary>
/// Represents a request to create an atomic Git commit scoped to task-defined file patterns.
/// </summary>
public sealed class CommitRequest
{
    /// <summary>
    /// The unique identifier for the task (e.g., "TSK-001").
    /// </summary>
    public required string TaskId { get; init; }

    /// <summary>
    /// The set of file path patterns that the task is allowed to modify (e.g., ["src/**/*.cs", "tests/**/*.cs"]).
    /// </summary>
    public required IReadOnlyList<string> FileScopes { get; init; }

    /// <summary>
    /// The list of file paths that have been changed and are candidates for staging.
    /// </summary>
    public required IReadOnlyList<string> ChangedFiles { get; init; }

    /// <summary>
    /// The summary message to include in the commit message (prefixed with task ID).
    /// </summary>
    public required string Summary { get; init; }
}
