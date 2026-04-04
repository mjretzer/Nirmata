using System.Text.Json.Serialization;

namespace nirmata.Data.Dto.Models.WorkspaceStatus;

/// <summary>
/// Workspace-scoped gate summary returned by <c>GET /v1/workspaces/{workspaceId}/status</c>.
/// Derives the current control-plane gate from canonical <c>.aos/spec</c>, <c>.aos/state</c>,
/// and <c>.aos/codebase</c> artifacts so clients do not need to infer workflow state themselves.
/// </summary>
public sealed class WorkspaceGateSummaryDto
{
    /// <summary>
    /// Identifier of the current blocking gate.
    /// One of the <see cref="WorkspaceGate"/> constants:
    /// <c>"interview"</c>, <c>"codebase-preflight"</c>, <c>"roadmap"</c>, <c>"planning"</c>,
    /// <c>"execution"</c>, <c>"verification"</c>, <c>"fix"</c>, or <c>"ready"</c>.
    /// </summary>
    [JsonPropertyName("currentGate")]
    public required string CurrentGate { get; init; }

    /// <summary>
    /// Artifact-backed explanation of why the workspace is currently blocked.
    /// <see langword="null"/> when <see cref="CurrentGate"/> is <c>"ready"</c>.
    /// </summary>
    [JsonPropertyName("blockingReason")]
    public string? BlockingReason { get; init; }

    /// <summary>
    /// The next CLI command or action the operator should run to advance the workspace.
    /// <see langword="null"/> when <see cref="CurrentGate"/> is <c>"ready"</c>.
    /// </summary>
    [JsonPropertyName("nextRequiredStep")]
    public string? NextRequiredStep { get; init; }

    /// <summary>
    /// Brownfield codebase readiness details.
    /// Present when the codebase map state is relevant to the current gate
    /// (i.e., when <c>map.json</c> is missing or stale).
    /// </summary>
    [JsonPropertyName("codebaseReadiness")]
    public CodebaseReadinessSummaryDto? CodebaseReadiness { get; init; }
}
