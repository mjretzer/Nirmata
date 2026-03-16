namespace nirmata.Agents.Execution.Execution.TaskExecutor;

/// <summary>
/// Represents a request to execute a task plan.
/// </summary>
public sealed class TaskExecutionRequest
{
    /// <summary>
    /// The unique identifier for the task (e.g., "TSK-001").
    /// </summary>
    public required string TaskId { get; init; }

    /// <summary>
    /// The path to the task directory containing plan.json.
    /// </summary>
    public required string TaskDirectory { get; init; }

    /// <summary>
    /// The set of file paths that the task is allowed to modify.
    /// </summary>
    public required IReadOnlyList<string> AllowedFileScope { get; init; }

    /// <summary>
    /// The correlation ID for tracing the execution flow.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// The parent run ID for nested execution tracking.
    /// </summary>
    public string? ParentRunId { get; init; }

    /// <summary>
    /// Additional context data for the execution.
    /// </summary>
    public Dictionary<string, object> ContextData { get; init; } = new();
}
