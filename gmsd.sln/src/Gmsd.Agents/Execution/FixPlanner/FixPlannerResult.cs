namespace Gmsd.Agents.Execution.FixPlanner;

/// <summary>
/// Represents the result of the Fix Planner workflow.
/// </summary>
public sealed record FixPlannerResult
{
    /// <summary>
    /// Indicates whether the fix planning succeeded.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// The list of generated fix task IDs (e.g., ["TSK-FIX-001", "TSK-FIX-002"]).
    /// </summary>
    public IReadOnlyList<string> FixTaskIds { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The detailed analysis of each issue and recommended fixes.
    /// </summary>
    public IReadOnlyList<IssueAnalysis> IssueAnalysis { get; init; } = Array.Empty<IssueAnalysis>();

    /// <summary>
    /// Canonical structured fix-plan JSON payload.
    /// </summary>
    public string? StructuredFixPlanJson { get; init; }

    /// <summary>
    /// Error message if the planning failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
