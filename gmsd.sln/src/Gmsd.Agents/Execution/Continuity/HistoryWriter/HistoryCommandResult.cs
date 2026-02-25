namespace Gmsd.Agents.Execution.Continuity.HistoryWriter;

/// <summary>
/// Result output from the write-history command.
/// Wraps the history entry data with command-specific context.
/// </summary>
public sealed class HistoryCommandResult
{
    /// <summary>
    /// The history entry that was written.
    /// </summary>
    public required HistoryEntry Entry { get; init; }

    /// <summary>
    /// The path to the summary.md file where the entry was appended.
    /// </summary>
    public required string SummaryPath { get; init; }

    /// <summary>
    /// Whether the entry was newly created or already existed.
    /// </summary>
    public bool WasAlreadyExists { get; init; }
}
