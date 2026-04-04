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
    /// Whether a task-level plan (.aos/spec/tasks/{taskId}/plan.json) exists for the current cursor position.
    /// Task plans are the only atomic execution contract — phase-level planning artifacts
    /// do not satisfy the execution gate.
    /// </summary>
    public required bool HasTaskPlan { get; init; }

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
    /// Whether the workspace has codebase intelligence present (.aos/codebase/map.json).
    /// </summary>
    public bool HasCodebaseIntelligence { get; init; }

    /// <summary>
    /// Whether the codebase intelligence is stale (older than staleness threshold).
    /// </summary>
    public bool IsCodebaseStale { get; init; }

    /// <summary>
    /// Whether all tasks in the current phase have been verified as passed.
    /// Used for next-phase progression after successful verification.
    /// </summary>
    public bool IsPhaseComplete { get; init; }

    /// <summary>
    /// Whether all phases in the current milestone have been completed.
    /// Used for milestone completion progression.
    /// </summary>
    public bool IsMilestoneComplete { get; init; }

    /// <summary>
    /// Additional workspace state data relevant for gating decisions.
    /// </summary>
    public Dictionary<string, object> StateData { get; init; } = new();
}
