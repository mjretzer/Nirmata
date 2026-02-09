namespace Gmsd.Agents.Execution.Backlog.DeferredIssuesCurator;

/// <summary>
/// Represents the routing decision for an issue.
/// </summary>
public enum RoutingDecision
{
    /// <summary>
    /// Issue should be routed into the main execution loop for immediate handling.
    /// </summary>
    MainLoop,

    /// <summary>
    /// Issue should remain deferred for later handling.
    /// </summary>
    Deferred,

    /// <summary>
    /// Issue should be discarded (not actionable, duplicate, or resolved).
    /// </summary>
    Discarded
}
