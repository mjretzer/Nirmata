using System.Text.Json.Serialization;

namespace nirmata.Data.Dto.Models.WorkspaceStatus;

/// <summary>
/// Brownfield codebase readiness details embedded in a workspace gate summary.
/// Present when the state of <c>.aos/codebase/map.json</c> is relevant to workflow progression.
/// </summary>
public sealed class CodebaseReadinessSummaryDto
{
    /// <summary>
    /// Freshness status of the codebase map.
    /// One of <c>"missing"</c>, <c>"stale"</c>, or <c>"ready"</c>.
    /// <c>"missing"</c> — <c>map.json</c> does not exist.
    /// <c>"stale"</c>   — <c>map.json</c> exists but is older than the staleness threshold.
    /// <c>"ready"</c>   — <c>map.json</c> exists and is within the freshness threshold.
    /// </summary>
    [JsonPropertyName("mapStatus")]
    public required string MapStatus { get; init; }

    /// <summary>Human-readable explanation of the readiness state.</summary>
    [JsonPropertyName("detail")]
    public required string Detail { get; init; }

    /// <summary>
    /// When <c>map.json</c> was last written; <see langword="null"/> when the file is absent.
    /// </summary>
    [JsonPropertyName("lastUpdated")]
    public DateTimeOffset? LastUpdated { get; init; }
}
