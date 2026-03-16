namespace nirmata.Agents.Execution.Planning.PhasePlanner.Assumptions;

/// <summary>
/// Represents an assumption identified during phase planning.
/// </summary>
public sealed record PhaseAssumption
{
    /// <summary>
    /// The unique identifier for this assumption.
    /// </summary>
    public required string AssumptionId { get; init; }

    /// <summary>
    /// The phase identifier this assumption relates to.
    /// </summary>
    public required string PhaseId { get; init; }

    /// <summary>
    /// The category of assumption (technical, business, resource, external).
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// The assumption statement (what is being assumed).
    /// </summary>
    public required string Statement { get; init; }

    /// <summary>
    /// The rationale for why this assumption is being made.
    /// </summary>
    public required string Rationale { get; init; }

    /// <summary>
    /// The potential impact if this assumption is incorrect.
    /// </summary>
    public string? ImpactIfIncorrect { get; init; }

    /// <summary>
    /// Suggested verification approach for this assumption.
    /// </summary>
    public string? VerificationApproach { get; init; }

    /// <summary>
    /// The source of this assumption (e.g., phase_brief, task_plan, llm_inference).
    /// </summary>
    public string Source { get; init; } = "unknown";

    /// <summary>
    /// When the assumption was identified.
    /// </summary>
    public DateTimeOffset IdentifiedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Confidence level in this assumption (high, medium, low).
    /// </summary>
    public string Confidence { get; init; } = "medium";
}
