using System.Text.Json.Serialization;

namespace nirmata.Data.Dto.Models.OrchestratorGate;

/// <summary>
/// Workspace-scoped orchestrator gate response.
/// Returned by <c>GET /v1/workspaces/{workspaceId}/orchestrator/gate</c>.
/// </summary>
public sealed class OrchestratorGateDto
{
    /// <summary>
    /// The task currently at the gate cursor, or <see langword="null"/> when no task
    /// is present in the workspace spec.
    /// </summary>
    [JsonPropertyName("taskId")]
    public string? TaskId { get; init; }

    /// <summary>Human-readable title for <see cref="TaskId"/>.</summary>
    [JsonPropertyName("taskTitle")]
    public string? TaskTitle { get; init; }

    /// <summary>
    /// <see langword="true"/> when all gate checks pass and the workspace is ready
    /// to advance (task is fully verified); <see langword="false"/> when at least one
    /// check has failed.
    /// </summary>
    [JsonPropertyName("runnable")]
    public required bool Runnable { get; init; }

    /// <summary>
    /// The next CLI command the operator should run to unblock or advance the workspace.
    /// <see langword="null"/> when all checks pass and the workspace is in a clean state.
    /// </summary>
    [JsonPropertyName("recommendedAction")]
    public string? RecommendedAction { get; init; }

    /// <summary>Ordered list of dependency, evidence, UAT, and workspace checks.</summary>
    [JsonPropertyName("checks")]
    public required IReadOnlyList<OrchestratorGateCheckDto> Checks { get; init; }
}
