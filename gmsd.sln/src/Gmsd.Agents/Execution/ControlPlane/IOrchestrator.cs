namespace Gmsd.Agents.Execution.ControlPlane;

/// <summary>
/// Interface for the orchestrator that serves as the unified entry point for agent workflow execution.
/// </summary>
public interface IOrchestrator
{
    /// <summary>
    /// Executes a workflow intent through the orchestration pipeline.
    /// </summary>
    /// <param name="intent">The workflow intent containing input and correlation ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the orchestration.</returns>
    Task<OrchestratorResult> ExecuteAsync(WorkflowIntent intent, CancellationToken ct = default);
}
