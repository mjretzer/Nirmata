namespace nirmata.Agents.Models.Contracts;

/// <summary>
/// Encapsulates parameters for initiating an agent run.
/// </summary>
public sealed class RunRequest
{
    /// <summary>
    /// Unique identifier for the run.
    /// If not provided, one will be generated.
    /// </summary>
    public string? RunId { get; set; }

    /// <summary>
    /// The workflow or agent name to execute.
    /// </summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>
    /// Input parameters for the workflow.
    /// </summary>
    public Dictionary<string, object> Inputs { get; set; } = new();

    /// <summary>
    /// Optional correlation ID for tracing.
    /// If not provided, one will be generated.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Optional timeout override in minutes.
    /// </summary>
    public int? TimeoutMinutes { get; set; }

    /// <summary>
    /// Optional metadata for the run.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
