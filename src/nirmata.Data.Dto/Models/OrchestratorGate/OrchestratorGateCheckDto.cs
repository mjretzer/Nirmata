using System.Text.Json.Serialization;

namespace nirmata.Data.Dto.Models.OrchestratorGate;

/// <summary>
/// A single check within an orchestrator gate response.
/// </summary>
public sealed class OrchestratorGateCheckDto
{
    /// <summary>Stable dotted identifier for the check (e.g. "workspace.project", "uat.status").</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Check category: "workspace", "dependency", "evidence", or "uat".
    /// </summary>
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    /// <summary>Short human-readable label for the check.</summary>
    [JsonPropertyName("label")]
    public required string Label { get; init; }

    /// <summary>Detailed description of the current check result or what is missing.</summary>
    [JsonPropertyName("detail")]
    public required string Detail { get; init; }

    /// <summary>
    /// Check outcome: <see cref="GateCheckStatus.Pass"/>, <see cref="GateCheckStatus.Fail"/>,
    /// or <see cref="GateCheckStatus.Warn"/>.
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }
}
