namespace nirmata.Data.Dto.Models.OrchestratorGate;

/// <summary>
/// Stable status constants for orchestrator gate checks.
/// Matches the check status values used across the API surface.
/// </summary>
public static class GateCheckStatus
{
    /// <summary>The check passed; no action required.</summary>
    public const string Pass = "pass";

    /// <summary>The check failed; execution is blocked until resolved.</summary>
    public const string Fail = "fail";

    /// <summary>The check raised a concern but is not blocking by itself.</summary>
    public const string Warn = "warn";
}
