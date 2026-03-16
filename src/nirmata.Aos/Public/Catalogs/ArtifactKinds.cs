namespace nirmata.Aos.Public.Catalogs;

/// <summary>
/// Stable catalog of AOS artifact kinds (spec/state/evidence).
/// </summary>
public static class ArtifactKinds
{
    /// <summary>
    /// Canonical kind label for milestone spec artifacts routed by IDs like <c>MS-0001</c>.
    /// </summary>
    public const string Milestone = "milestone";

    /// <summary>
    /// Canonical kind label for phase spec artifacts routed by IDs like <c>PH-0001</c>.
    /// </summary>
    public const string Phase = "phase";

    /// <summary>
    /// Canonical kind label for task spec artifacts routed by IDs like <c>TSK-000001</c>.
    /// </summary>
    public const string Task = "task";

    /// <summary>
    /// Canonical kind label for issue spec artifacts routed by IDs like <c>ISS-0001</c>.
    /// </summary>
    public const string Issue = "issue";

    /// <summary>
    /// Canonical kind label for UAT spec artifacts routed by IDs like <c>UAT-0001</c>.
    /// </summary>
    public const string Uat = "uat";

    /// <summary>
    /// Canonical kind label for run evidence artifacts routed by RUN IDs (current engine format).
    /// </summary>
    public const string Run = "run";

    /// <summary>
    /// Canonical kind label for context pack artifacts routed by IDs like <c>PCK-0001</c>.
    ///
    /// Note: context packs are not currently roadmap item kinds.
    /// </summary>
    public const string ContextPack = "context-pack";

    // Accepted aliases (kept for tolerant parsing / human-authored roadmap content).
    public const string Milestones = "milestones";
    public const string Phases = "phases";
    public const string Tasks = "tasks";
    public const string Issues = "issues";
    public const string Runs = "runs";

    /// <summary>
    /// Canonical kind labels for roadmap items and tooling.
    /// </summary>
    public static readonly IReadOnlyList<string> CanonicalRoadmapItemKinds =
    [
        Milestone,
        Phase,
        Task,
        Issue,
        Uat,
        Run
    ];

    /// <summary>
    /// All recognized kind labels (canonical + tolerated aliases).
    /// </summary>
    public static readonly IReadOnlyList<string> RecognizedRoadmapItemKinds =
    [
        Milestone,
        Milestones,
        Phase,
        Phases,
        Task,
        Tasks,
        Issue,
        Issues,
        Uat,
        Run,
        Runs
    ];
}

