namespace nirmata.Agents.Execution.Continuity.ProgressReporter;

/// <summary>
/// Generates deterministic progress reports from current execution state.
/// Reads from IStateStore and produces structured progress output.
/// </summary>
public interface IProgressReporter
{
    /// <summary>
    /// Generates a progress report from the current execution state.
    /// </summary>
    /// <param name="format">Output format ("json" or "markdown"). Defaults to "json".</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The progress report.</returns>
    Task<ProgressReport> ReportAsync(string format = "json", CancellationToken ct = default);

    /// <summary>
    /// Generates a progress report as a formatted string.
    /// </summary>
    /// <param name="format">Output format ("json" or "markdown"). Defaults to "json".</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The progress report as a formatted string.</returns>
    Task<string> ReportAsStringAsync(string format = "json", CancellationToken ct = default);
}
