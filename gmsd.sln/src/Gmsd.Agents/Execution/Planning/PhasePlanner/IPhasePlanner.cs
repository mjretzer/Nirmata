namespace Gmsd.Agents.Execution.Planning.PhasePlanner;

/// <summary>
/// Defines the contract for planning tasks within a phase.
/// </summary>
public interface IPhasePlanner
{
    /// <summary>
    /// Creates a task plan from a phase brief by decomposing the phase into atomic tasks.
    /// </summary>
    /// <param name="brief">The phase brief containing context and requirements.</param>
    /// <param name="runId">The current run identifier for correlation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task plan containing decomposed tasks with file scopes and verification steps.</returns>
    Task<TaskPlan> CreateTaskPlanAsync(PhaseBrief brief, string runId, CancellationToken ct = default);
}
