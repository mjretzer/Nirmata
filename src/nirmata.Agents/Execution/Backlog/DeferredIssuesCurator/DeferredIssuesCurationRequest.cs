namespace nirmata.Agents.Execution.Backlog.DeferredIssuesCurator;

/// <summary>
/// Represents a request to the Deferred Issues Curator to triage pending issues.
/// </summary>
public sealed record DeferredIssuesCurationRequest
{
    /// <summary>
    /// The root path of the workspace containing the .aos directory.
    /// </summary>
    public required string WorkspaceRoot { get; init; }

    /// <summary>
    /// Optional filter to only triage specific issue IDs. If empty, all pending issues are triaged.
    /// </summary>
    public IReadOnlyList<string> IssueIds { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The minimum severity level to consider for main loop routing. Issues below this severity are deferred.
    /// Defaults to "high".
    /// </summary>
    public string MinimumSeverityForMainLoop { get; init; } = "high";

    /// <summary>
    /// If true, updates the ISS-*.json files with triage decisions. If false, only returns recommendations.
    /// </summary>
    public bool ApplyDecisions { get; init; } = true;

    /// <summary>
    /// If true, writes triage events to .aos/state/events.ndjson.
    /// </summary>
    public bool WriteEvents { get; init; } = true;
}
