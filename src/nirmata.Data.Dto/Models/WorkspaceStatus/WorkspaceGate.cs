namespace nirmata.Data.Dto.Models.WorkspaceStatus;

/// <summary>
/// Well-known current gate identifiers returned by the workspace gate summary endpoint.
/// Values align with the orchestrator gate sequence documented in <c>docs/workflows/gating.md</c>.
/// </summary>
public static class WorkspaceGate
{
    /// <summary>Project specification is absent — the new-project interview is required.</summary>
    public const string Interview = "interview";

    /// <summary>
    /// Codebase intelligence is missing or stale and a roadmap or task plan does not yet exist.
    /// Brownfield preflight (map-codebase) must run before planning can proceed.
    /// </summary>
    public const string CodebasePreflight = "codebase-preflight";

    /// <summary>Project spec exists but a roadmap has not been created yet.</summary>
    public const string Roadmap = "roadmap";

    /// <summary>Roadmap exists but the current phase has no task plans yet.</summary>
    public const string Planning = "planning";

    /// <summary>Task plans exist but execution evidence is absent — execute-plan is required.</summary>
    public const string Execution = "execution";

    /// <summary>Execution evidence is present but UAT verification has not been completed.</summary>
    public const string Verification = "verification";

    /// <summary>Verification failed or execution failed — a fix plan is required.</summary>
    public const string Fix = "fix";

    /// <summary>All gate checks pass; the workspace is in a clean, ready state.</summary>
    public const string Ready = "ready";
}
