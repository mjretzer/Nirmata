namespace Gmsd.Agents.Execution.FixPlanner;

/// <summary>
/// Defines the contract for the Fix Planner workflow.
/// Analyzes UAT verification failures and generates targeted fix task plans.
/// </summary>
public interface IFixPlanner
{
    /// <summary>
    /// Analyzes verification failures and generates fix task plans.
    /// </summary>
    /// <param name="request">The fix planner request containing issue IDs and context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The fix planner result with generated fix tasks and issue analysis.</returns>
    Task<FixPlannerResult> PlanFixesAsync(FixPlannerRequest request, CancellationToken ct = default);
}
