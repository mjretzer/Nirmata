namespace nirmata.Agents.Execution.Execution.SubagentRuns;

/// <summary>
/// Represents a request to run a subagent with fresh context isolation.
/// </summary>
public sealed class SubagentRunRequest
{
    /// <summary>
    /// The unique identifier for the subagent run.
    /// </summary>
    public required string RunId { get; init; }

    /// <summary>
    /// The task ID associated with this subagent run.
    /// </summary>
    public required string TaskId { get; init; }

    /// <summary>
    /// The subagent configuration name to use.
    /// </summary>
    public required string SubagentConfig { get; init; }

    /// <summary>
    /// The context pack IDs to load for the subagent.
    /// </summary>
    public IReadOnlyList<string> ContextPackIds { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The budget limits for the subagent execution.
    /// </summary>
    public SubagentBudget Budget { get; init; } = new();

    /// <summary>
    /// The set of file paths that the subagent is allowed to modify.
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
    /// Additional context data for the subagent.
    /// </summary>
    public Dictionary<string, object> ContextData { get; init; } = new();
}

/// <summary>
/// Budget configuration for subagent execution.
/// </summary>
public sealed class SubagentBudget
{
    /// <summary>
    /// Maximum number of iterations allowed.
    /// </summary>
    public int MaxIterations { get; init; } = 10;

    /// <summary>
    /// Maximum number of tool calls allowed.
    /// </summary>
    public int MaxToolCalls { get; init; } = 50;

    /// <summary>
    /// Maximum execution time in seconds.
    /// </summary>
    public int MaxExecutionTimeSeconds { get; init; } = 300;

    /// <summary>
    /// Maximum token budget for LLM calls.
    /// </summary>
    public int MaxTokens { get; init; } = 100000;
}
