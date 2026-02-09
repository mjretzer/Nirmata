namespace Gmsd.Agents.Execution.Execution.TaskExecutor;

/// <summary>
/// Executes task plans sequentially with strict file-scoping, per-task subagent isolation, and comprehensive evidence capture.
/// </summary>
public interface ITaskExecutor
{
    /// <summary>
    /// Executes a task plan from the specified task directory.
    /// </summary>
    /// <param name="request">The task execution request containing task ID, scope, and configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The task execution result with normalized output and execution metadata.</returns>
    Task<TaskExecutionResult> ExecuteAsync(TaskExecutionRequest request, CancellationToken ct = default);
}
