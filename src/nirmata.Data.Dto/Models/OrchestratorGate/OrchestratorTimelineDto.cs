namespace nirmata.Data.Dto.Models.OrchestratorGate;

/// <summary>
/// Ordered orchestrator timeline for a workspace.
/// Returned by <c>GET /v1/workspaces/{workspaceId}/orchestrator/timeline</c>.
/// </summary>
public sealed class OrchestratorTimelineDto
{
    /// <summary>
    /// Timeline steps in workspace progression order (earliest phase first).
    /// Each step corresponds to a phase in <c>.aos/spec/phases/</c>.
    /// </summary>
    public required IReadOnlyList<OrchestratorTimelineStepDto> Steps { get; init; }
}
