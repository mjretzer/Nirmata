namespace nirmata.Agents.Execution.Execution.SubagentRuns;

/// <summary>
/// Orchestrates subagent runs with fresh context isolation and evidence capture.
/// </summary>
public interface ISubagentOrchestrator
{
    /// <summary>
    /// Runs a subagent with the specified request and returns the execution result.
    /// </summary>
    /// <param name="request">The subagent run request containing configuration and context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The subagent run result with execution output and metadata.</returns>
    Task<SubagentRunResult> RunSubagentAsync(SubagentRunRequest request, CancellationToken ct = default);
}
