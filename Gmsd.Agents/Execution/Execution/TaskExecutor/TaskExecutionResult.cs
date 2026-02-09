namespace Gmsd.Agents.Execution.Execution.TaskExecutor;

/// <summary>
/// Represents the result of a task execution.
/// </summary>
public sealed class TaskExecutionResult
{
    /// <summary>
    /// Whether the task execution succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// The unique identifier of the created run record.
    /// </summary>
    public required string RunId { get; init; }

    /// <summary>
    /// Normalized output from the task execution.
    /// </summary>
    public string? NormalizedOutput { get; init; }

    /// <summary>
    /// Deterministic hash of the execution output for verification.
    /// </summary>
    public string? DeterministicHash { get; init; }

    /// <summary>
    /// Error message if the execution failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// List of files that were modified during execution.
    /// </summary>
    public IReadOnlyList<string> ModifiedFiles { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Evidence artifacts captured during execution.
    /// </summary>
    public IReadOnlyList<string> EvidenceArtifacts { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Additional result data from the execution.
    /// </summary>
    public Dictionary<string, object> ResultData { get; init; } = new();
}
