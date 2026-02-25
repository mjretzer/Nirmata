namespace Gmsd.Agents.Execution.Execution.AtomicGitCommitter;

/// <summary>
/// Statistics about a Git diff operation.
/// </summary>
public sealed class DiffStat
{
    /// <summary>
    /// The number of files changed.
    /// </summary>
    public required int FilesChanged { get; init; }

    /// <summary>
    /// The number of lines inserted.
    /// </summary>
    public required int Insertions { get; init; }

    /// <summary>
    /// The number of lines deleted.
    /// </summary>
    public required int Deletions { get; init; }
}
