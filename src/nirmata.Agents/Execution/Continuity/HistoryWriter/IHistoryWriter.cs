namespace nirmata.Agents.Execution.Continuity.HistoryWriter;

/// <summary>
/// Appends durable narrative history entries to .aos/spec/summary.md.
/// Links runs and tasks to their evidence artifacts and commit hashes.
/// </summary>
public interface IHistoryWriter
{
    /// <summary>
    /// Appends a history entry for a completed task or run.
    /// </summary>
    /// <param name="runId">The run ID to write history for.</param>
    /// <param name="taskId">Optional task ID. If null, writes run-level entry.</param>
    /// <param name="narrative">Optional narrative description.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The history entry that was appended.</returns>
    /// <exception cref="FileNotFoundException">Thrown when evidence does not exist for the run/task.</exception>
    Task<HistoryEntry> AppendAsync(string runId, string? taskId = null, string? narrative = null, CancellationToken ct = default);

    /// <summary>
    /// Checks if history exists for a given run/task.
    /// </summary>
    /// <param name="runId">The run ID to check.</param>
    /// <param name="taskId">Optional task ID. If null, checks run-level entry.</param>
    /// <returns>True if history entry exists.</returns>
    bool Exists(string runId, string? taskId = null);

    /// <summary>
    /// Gets the full path to the summary.md file.
    /// </summary>
    string SummaryPath { get; }
}
