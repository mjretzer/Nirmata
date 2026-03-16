namespace nirmata.Agents.Execution.Backlog.DeferredIssuesCurator;

/// <summary>
/// Represents a routing recommendation for a single issue.
/// </summary>
public sealed record IssueRoutingRecommendation
{
    /// <summary>
    /// The issue ID (e.g., "ISS-001").
    /// </summary>
    public required string IssueId { get; init; }

    /// <summary>
    /// The assessed severity level of the issue.
    /// </summary>
    public required string Severity { get; init; }

    /// <summary>
    /// The routing decision for this issue.
    /// </summary>
    public required RoutingDecision Decision { get; init; }

    /// <summary>
    /// The rationale explaining why this routing decision was made.
    /// </summary>
    public required string Rationale { get; init; }

    /// <summary>
    /// The timestamp when the triage occurred (ISO 8601).
    /// </summary>
    public required string TriagedAt { get; init; }

    /// <summary>
    /// Indicates whether the issue file was successfully updated with the triage decision.
    /// </summary>
    public bool IssueFileUpdated { get; init; }

    /// <summary>
    /// Indicates whether the triage event was successfully written to events.ndjson.
    /// </summary>
    public bool EventWritten { get; init; }
}
