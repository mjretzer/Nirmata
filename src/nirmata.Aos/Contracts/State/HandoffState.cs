using System.Text.Json.Serialization;

namespace nirmata.Aos.Contracts.State;

/// <summary>
/// Stable contract representing <c>.aos/state/handoff.json</c>.
/// Captures complete execution state for deterministic resumption.
/// </summary>
public sealed record HandoffState
{
    /// <summary>
    /// Schema version for compatibility and migration support.
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; init; } = "1.0";

    /// <summary>
    /// ISO8601 timestamp when the handoff was created.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; init; }

    /// <summary>
    /// The source run ID for provenance tracking.
    /// </summary>
    [JsonPropertyName("sourceRunId")]
    public required string SourceRunId { get; init; }

    /// <summary>
    /// Optional continuation run ID if this handoff was used for resumption.
    /// </summary>
    [JsonPropertyName("continuationRunId")]
    public string? ContinuationRunId { get; init; }

    /// <summary>
    /// Current cursor position (phase, milestone, task IDs).
    /// </summary>
    [JsonPropertyName("cursor")]
    public required StateCursor Cursor { get; init; }

    /// <summary>
    /// In-flight task context including partial results.
    /// </summary>
    [JsonPropertyName("taskContext")]
    public required TaskContext TaskContext { get; init; }

    /// <summary>
    /// Scope constraints preserved from the original execution.
    /// </summary>
    [JsonPropertyName("scope")]
    public required ScopeConstraints Scope { get; init; }

    /// <summary>
    /// The next pending command to execute after resumption.
    /// </summary>
    [JsonPropertyName("nextCommand")]
    public required NextCommand NextCommand { get; init; }

    /// <summary>
    /// Optional reason or message for the pause.
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}
