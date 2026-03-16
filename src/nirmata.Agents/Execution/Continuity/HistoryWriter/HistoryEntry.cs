using System.Text.Json.Serialization;

namespace nirmata.Agents.Execution.Continuity.HistoryWriter;

/// <summary>
/// Represents the verification proof for a history entry.
/// </summary>
public sealed record VerificationProof
{
    /// <summary>
    /// The verification status (e.g., "passed", "failed", "pending", "skipped").
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>
    /// The verification method used (e.g., "uat-verifier", "unit-tests", "manual").
    /// </summary>
    [JsonPropertyName("method")]
    public required string Method { get; init; }

    /// <summary>
    /// Number of issues found during verification (for failed status).
    /// </summary>
    [JsonPropertyName("issues")]
    public int? Issues { get; init; }

    /// <summary>
    /// Additional details about the verification.
    /// </summary>
    [JsonPropertyName("details")]
    public string? Details { get; init; }
}

/// <summary>
/// Represents an evidence pointer linking to an artifact.
/// </summary>
public sealed record EvidencePointer
{
    /// <summary>
    /// The type of evidence (e.g., "summary", "verification", "logs", "artifacts").
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>
    /// Relative path from workspace root to the evidence artifact.
    /// Uses forward slashes for cross-platform compatibility.
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    /// <summary>
    /// Optional description of the evidence.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

/// <summary>
/// History entry schema for durable narrative records of completed work.
/// Entries are appended to .aos/spec/summary.md.
/// </summary>
public sealed record HistoryEntry
{
    /// <summary>
    /// Schema version for forward compatibility.
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; init; } = "1.0";

    /// <summary>
    /// RUN/TSK key (e.g., "RUN-0001/TSK-0003" or "RUN-0001" for run-level entries).
    /// </summary>
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    /// <summary>
    /// ISO8601 timestamp of entry creation.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; init; }

    /// <summary>
    /// Verification proof with status and method.
    /// </summary>
    [JsonPropertyName("verification")]
    public required VerificationProof Verification { get; init; }

    /// <summary>
    /// Git commit hash when available from git context. Null if unavailable.
    /// </summary>
    [JsonPropertyName("commitHash")]
    public string? CommitHash { get; init; }

    /// <summary>
    /// Evidence pointers linking to artifacts in .aos/evidence/.
    /// </summary>
    [JsonPropertyName("evidence")]
    public IReadOnlyList<EvidencePointer> Evidence { get; init; } = Array.Empty<EvidencePointer>();

    /// <summary>
    /// Optional brief narrative description.
    /// </summary>
    [JsonPropertyName("narrative")]
    public string? Narrative { get; init; }

    /// <summary>
    /// The run ID associated with this entry.
    /// </summary>
    [JsonPropertyName("runId")]
    public required string RunId { get; init; }

    /// <summary>
    /// The task ID associated with this entry (null for run-level entries).
    /// </summary>
    [JsonPropertyName("taskId")]
    public string? TaskId { get; init; }
}
