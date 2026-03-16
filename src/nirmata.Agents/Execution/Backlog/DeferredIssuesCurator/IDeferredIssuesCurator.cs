namespace nirmata.Agents.Execution.Backlog.DeferredIssuesCurator;

/// <summary>
/// Defines the contract for the Deferred Issues Curator.
/// Triages issues from .aos/spec/issues/ and routes urgent items into the main execution loop.
/// </summary>
public interface IDeferredIssuesCurator
{
    /// <summary>
    /// Triages pending issues and returns routing recommendations.
    /// </summary>
    /// <param name="request">The curation request containing workspace path and options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The curation result with routing recommendations for each issue.</returns>
    Task<DeferredIssuesCurationResult> CurateAsync(DeferredIssuesCurationRequest request, CancellationToken ct = default);
}
