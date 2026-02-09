namespace Gmsd.Agents.Models.Results;

/// <summary>
/// Encapsulates the result data from an agent run.
/// </summary>
public sealed class RunResponse
{
    /// <summary>
    /// Unique identifier for the run.
    /// </summary>
    public string RunId { get; set; } = string.Empty;

    /// <summary>
    /// Correlation ID used for tracing.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the run completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Output data from the run.
    /// </summary>
    public Dictionary<string, object> Outputs { get; set; } = new();

    /// <summary>
    /// Error message if the run failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Error code if the run failed.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Timestamp when the run started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Timestamp when the run completed.
    /// </summary>
    public DateTimeOffset CompletedAt { get; set; }

    /// <summary>
    /// Duration of the run.
    /// </summary>
    public TimeSpan Duration => CompletedAt - StartedAt;
}
