namespace Gmsd.Agents.Execution.FixPlanner;

/// <summary>
/// Represents a request to the Fix Planner to analyze issues and generate fix plans.
/// </summary>
public sealed record FixPlannerRequest
{
    /// <summary>
    /// The list of issue IDs to analyze (e.g., ["ISS-001", "ISS-002"]).
    /// </summary>
    public required IReadOnlyList<string> IssueIds { get; init; }

    /// <summary>
    /// The root path of the workspace containing the repository.
    /// </summary>
    public required string WorkspaceRoot { get; init; }

    /// <summary>
    /// The parent task ID that generated these issues.
    /// </summary>
    public required string ParentTaskId { get; init; }

    /// <summary>
    /// The context pack ID for loading relevant codebase context.
    /// </summary>
    public required string ContextPackId { get; init; }
}
