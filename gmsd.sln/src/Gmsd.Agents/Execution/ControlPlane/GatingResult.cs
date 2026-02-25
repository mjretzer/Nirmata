namespace Gmsd.Agents.Execution.ControlPlane;

/// <summary>
/// Represents the result of a gating engine evaluation.
/// </summary>
public sealed class GatingResult
{
    /// <summary>
    /// One of the six phase names: Interviewer, Roadmapper, Planner, Executor, Verifier, FixPlanner.
    /// </summary>
    public required string TargetPhase { get; init; }

    /// <summary>
    /// Human-readable explanation for the routing decision.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Detailed reasoning explaining why this phase was selected.
    /// </summary>
    public string? Reasoning { get; init; }

    /// <summary>
    /// Structured description of the proposed action for user confirmation.
    /// </summary>
    public ProposedAction? ProposedAction { get; init; }

    /// <summary>
    /// Whether this action requires user confirmation before execution.
    /// </summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>
    /// Relevant workspace state snapshots for the handler.
    /// </summary>
    public Dictionary<string, object> ContextData { get; init; } = new();
}
