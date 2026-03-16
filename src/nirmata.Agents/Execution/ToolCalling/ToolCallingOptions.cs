namespace nirmata.Agents.Execution.ToolCalling;

/// <summary>
/// Options for controlling the behavior of the tool calling loop.
/// </summary>
public sealed record ToolCallingOptions
{
    /// <summary>
    /// Maximum number of iterations (LLM calls) allowed in the loop.
    /// Default is 10 to prevent infinite loops.
    /// </summary>
    public int MaxIterations { get; init; } = 10;

    /// <summary>
    /// Maximum time allowed for the entire loop to complete.
    /// Default is 5 minutes.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum number of tool calls allowed across all iterations.
    /// Default is 50 to prevent excessive costs.
    /// </summary>
    public int MaxToolCalls { get; init; } = 50;

    /// <summary>
    /// Maximum total tokens allowed across all iterations.
    /// Null means no limit.
    /// </summary>
    public int? MaxTotalTokens { get; init; }

    /// <summary>
    /// Temperature setting for LLM sampling. Lower values make output more deterministic.
    /// Null means use provider default.
    /// </summary>
    public float? Temperature { get; init; }

    /// <summary>
    /// Maximum tokens per LLM completion. Null means use provider default.
    /// </summary>
    public int? MaxTokensPerCompletion { get; init; }

    /// <summary>
    /// Model identifier to use for completions. Null means use configured default.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// When set to a specific tool name, forces the model to use that tool on the first turn.
    /// </summary>
    public string? ToolChoice { get; init; }

    /// <summary>
    /// Whether to enable parallel tool call execution within a single turn.
    /// Default is true.
    /// </summary>
    public bool EnableParallelToolExecution { get; init; } = true;

    /// <summary>
    /// Maximum number of concurrent tool executions when parallel execution is enabled.
    /// Default is 32.
    /// </summary>
    public int MaxParallelToolExecutions { get; init; } = 32;
}
