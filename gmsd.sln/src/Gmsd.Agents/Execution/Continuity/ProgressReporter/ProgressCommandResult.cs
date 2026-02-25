namespace Gmsd.Agents.Execution.Continuity.ProgressReporter;

/// <summary>
/// Result output from the report-progress command.
/// Wraps the progress report data with command-specific context.
/// </summary>
public sealed class ProgressCommandResult
{
    /// <summary>
    /// The progress report data.
    /// </summary>
    public required ProgressReport Report { get; init; }

    /// <summary>
    /// The output format used ("json" or "markdown").
    /// </summary>
    public string Format { get; init; } = "json";

    /// <summary>
    /// The formatted output string.
    /// </summary>
    public string? FormattedOutput { get; init; }
}
