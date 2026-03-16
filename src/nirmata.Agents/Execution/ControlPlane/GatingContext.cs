namespace nirmata.Agents.Execution.ControlPlane;

/// <summary>
/// Carries workspace state snapshot for gating evaluation.
/// </summary>
public sealed class GatingContext
{
    /// <summary>
    /// Whether the workspace has a project specification.
    /// </summary>
    public required bool HasProject { get; init; }

    /// <summary>
    /// Whether the workspace has a roadmap defined.
    /// </summary>
    public required bool HasRoadmap { get; init; }

    /// <summary>
    /// Whether the workspace has a plan at the current cursor position.
    /// </summary>
    public required bool HasPlan { get; init; }

    /// <summary>
    /// The current cursor position/state.
    /// </summary>
    public string? CurrentCursor { get; init; }

    /// <summary>
    /// Status of the last execution (e.g., "completed", "failed", "pending").
    /// </summary>
    public string? LastExecutionStatus { get; init; }

    /// <summary>
    /// Status of the last verification (e.g., "passed", "failed", null).
    /// </summary>
    public string? LastVerificationStatus { get; init; }

    /// <summary>
    /// Issue IDs discovered from failed verification for FixPlanner routing.
    /// </summary>
    public IReadOnlyList<string> IssueIds { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Parent task ID for fix planning context.
    /// </summary>
    public string? ParentTaskId { get; init; }

    /// <summary>
    /// Additional workspace state data relevant for gating decisions.
    /// </summary>
    public Dictionary<string, object> StateData { get; init; } = new();
}
