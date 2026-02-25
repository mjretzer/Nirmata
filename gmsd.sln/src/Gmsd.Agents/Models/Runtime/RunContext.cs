namespace Gmsd.Agents.Models.Runtime;

/// <summary>
/// Execution context for an agent run.
/// </summary>
public sealed class RunContext
{
    /// <summary>
    /// Unique identifier for the run.
    /// </summary>
    public string RunId { get; set; } = string.Empty;

    /// <summary>
    /// Correlation ID for tracing.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// The workflow or agent name being executed.
    /// </summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>
    /// Input parameters for the workflow.
    /// </summary>
    public Dictionary<string, object> Inputs { get; set; } = new();

    /// <summary>
    /// When the run started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timeout for the run.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Metadata for the run.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Cancellation token for the run.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>
    /// Current step or phase of execution.
    /// </summary>
    public string CurrentStep { get; set; } = string.Empty;

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public int ProgressPercent { get; set; }
}
