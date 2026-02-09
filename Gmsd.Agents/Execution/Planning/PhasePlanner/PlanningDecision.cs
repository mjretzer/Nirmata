namespace Gmsd.Agents.Execution.Planning.PhasePlanner;

/// <summary>
/// Represents a planning decision captured during phase planning.
/// </summary>
public sealed record PlanningDecision
{
    /// <summary>
    /// The unique identifier for this decision.
    /// </summary>
    public required string DecisionId { get; init; }

    /// <summary>
    /// The phase identifier this decision relates to.
    /// </summary>
    public required string PhaseId { get; init; }

    /// <summary>
    /// The run identifier that made this decision.
    /// </summary>
    public required string RunId { get; init; }

    /// <summary>
    /// The type of decision made (e.g., task_decomposition, scope_boundary, approach_selection).
    /// </summary>
    public required string DecisionType { get; init; }

    /// <summary>
    /// A clear statement of what was decided.
    /// </summary>
    public required string Statement { get; init; }

    /// <summary>
    /// The rationale or reasoning behind the decision.
    /// </summary>
    public required string Rationale { get; init; }

    /// <summary>
    /// Alternatives that were considered but not chosen.
    /// </summary>
    public IReadOnlyList<string> AlternativesConsidered { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Assumptions that underlie this decision.
    /// </summary>
    public IReadOnlyList<string> UnderlyingAssumptions { get; init; } = Array.Empty<string>();

    /// <summary>
    /// When the decision was made.
    /// </summary>
    public DateTimeOffset DecidedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The planning context at the time of the decision.
    /// </summary>
    public PlanningContext Context { get; init; } = new();
}

/// <summary>
/// Context information for a planning decision.
/// </summary>
public sealed record PlanningContext
{
    /// <summary>
    /// Input factors that influenced the decision.
    /// </summary>
    public IReadOnlyList<string> InputFactors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Constraints that affected the decision.
    /// </summary>
    public IReadOnlyList<string> Constraints { get; init; } = Array.Empty<string>();

    /// <summary>
    /// References to relevant documentation or specifications.
    /// </summary>
    public IReadOnlyList<string> References { get; init; } = Array.Empty<string>();
}
