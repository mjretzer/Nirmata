namespace Gmsd.Agents.Execution.Backlog.DeferredIssuesCurator;

/// <summary>
/// Represents the result of the Deferred Issues Curator workflow.
/// </summary>
public sealed record DeferredIssuesCurationResult
{
    /// <summary>
    /// Indicates whether the curation succeeded.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// The list of routing recommendations for each triaged issue.
    /// </summary>
    public IReadOnlyList<IssueRoutingRecommendation> Recommendations { get; init; } = Array.Empty<IssueRoutingRecommendation>();

    /// <summary>
    /// The count of issues routed to the main execution loop.
    /// </summary>
    public int MainLoopCount => Recommendations.Count(r => r.Decision == RoutingDecision.MainLoop);

    /// <summary>
    /// The count of issues deferred for later handling.
    /// </summary>
    public int DeferredCount => Recommendations.Count(r => r.Decision == RoutingDecision.Deferred);

    /// <summary>
    /// The count of issues discarded (not actionable or duplicates).
    /// </summary>
    public int DiscardedCount => Recommendations.Count(r => r.Decision == RoutingDecision.Discarded);

    /// <summary>
    /// Error message if the curation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
