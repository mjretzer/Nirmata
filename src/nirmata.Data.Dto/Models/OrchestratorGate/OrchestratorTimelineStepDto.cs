namespace nirmata.Data.Dto.Models.OrchestratorGate;

/// <summary>
/// A single step in the workspace orchestrator timeline (typically one phase).
/// </summary>
public sealed class OrchestratorTimelineStepDto
{
    /// <summary>Stable step identifier (typically the phase id, e.g. "PH-0001").</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable step label (phase title or id fallback).</summary>
    public required string Label { get; init; }

    /// <summary>
    /// Step status: "completed", "active", or "pending".
    /// Derived from the phase's own status field and the workspace cursor position.
    /// </summary>
    public required string Status { get; init; }
}
