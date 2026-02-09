namespace Gmsd.Agents.Execution.Execution.SubagentRuns;

/// <summary>
/// Represents the result of a subagent run.
/// </summary>
public sealed class SubagentRunResult
{
    /// <summary>
    /// Whether the subagent run succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// The unique identifier of the created run record.
    /// </summary>
    public required string RunId { get; init; }

    /// <summary>
    /// The task ID associated with this subagent run.
    /// </summary>
    public required string TaskId { get; init; }

    /// <summary>
    /// Normalized output from the subagent execution.
    /// </summary>
    public string? NormalizedOutput { get; init; }

    /// <summary>
    /// Deterministic hash of the execution output for verification.
    /// </summary>
    public string? DeterministicHash { get; init; }

    /// <summary>
    /// Error message if the subagent run failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// List of files that were modified during the subagent run.
    /// </summary>
    public IReadOnlyList<string> ModifiedFiles { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Evidence artifacts captured during the subagent run.
    /// </summary>
    public IReadOnlyList<string> EvidenceArtifacts { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Tool calls made during the subagent run.
    /// </summary>
    public IReadOnlyList<SubagentToolCall> ToolCalls { get; init; } = Array.Empty<SubagentToolCall>();

    /// <summary>
    /// Execution metrics for the subagent run.
    /// </summary>
    public SubagentExecutionMetrics Metrics { get; init; } = new();

    /// <summary>
    /// Additional result data from the subagent run.
    /// </summary>
    public Dictionary<string, object> ResultData { get; init; } = new();
}

/// <summary>
/// Represents a tool call made during subagent execution.
/// </summary>
public sealed class SubagentToolCall
{
    /// <summary>
    /// The name of the tool that was called.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// The arguments passed to the tool.
    /// </summary>
    public required string Arguments { get; init; }

    /// <summary>
    /// The result returned by the tool.
    /// </summary>
    public string? Result { get; init; }

    /// <summary>
    /// The timestamp when the tool was called.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Execution metrics for a subagent run.
/// </summary>
public sealed class SubagentExecutionMetrics
{
    /// <summary>
    /// Number of iterations executed.
    /// </summary>
    public int IterationCount { get; init; }

    /// <summary>
    /// Number of tool calls made.
    /// </summary>
    public int ToolCallCount { get; init; }

    /// <summary>
    /// Total execution time in milliseconds.
    /// </summary>
    public long ExecutionTimeMs { get; init; }

    /// <summary>
    /// Total tokens consumed.
    /// </summary>
    public int TokensConsumed { get; init; }
}
