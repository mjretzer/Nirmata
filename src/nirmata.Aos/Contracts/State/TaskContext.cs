using System.Text.Json.Serialization;

namespace nirmata.Aos.Contracts.State;

/// <summary>
/// Represents the task context captured during a handoff.
/// </summary>
public sealed record TaskContext
{
    /// <summary>
    /// The unique task identifier.
    /// </summary>
    [JsonPropertyName("taskId")]
    public required string TaskId { get; init; }

    /// <summary>
    /// The current status of the task.
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>
    /// Partial results accumulated during task execution.
    /// </summary>
    [JsonPropertyName("partialResults")]
    public Dictionary<string, object>? PartialResults { get; init; }

    /// <summary>
    /// The execution packet reference for reconstructing the task context.
    /// </summary>
    [JsonPropertyName("executionPacket")]
    public ExecutionPacketRef? ExecutionPacket { get; init; }
}

/// <summary>
/// Reference to an execution packet stored in the evidence folder.
/// </summary>
public sealed record ExecutionPacketRef
{
    /// <summary>
    /// The run ID where the packet is stored.
    /// </summary>
    [JsonPropertyName("runId")]
    public required string RunId { get; init; }

    /// <summary>
    /// The relative path to the packet file within the evidence folder.
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    /// <summary>
    /// The packet content hash for integrity verification.
    /// </summary>
    [JsonPropertyName("hash")]
    public string? Hash { get; init; }
}
