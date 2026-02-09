using System.Text.Json.Serialization;

namespace Gmsd.Agents.Execution.Continuity.ProgressReporter;

/// <summary>
/// Represents a blocker detected during execution with severity and description.
/// </summary>
public sealed record Blocker
{
    /// <summary>
    /// The task ID associated with this blocker, if applicable.
    /// </summary>
    [JsonPropertyName("task")]
    public string? Task { get; init; }

    /// <summary>
    /// The type of blocker (e.g., "verification-failed", "dependency-missing", "error").
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>
    /// The severity level of the blocker (e.g., "low", "medium", "high", "critical").
    /// </summary>
    [JsonPropertyName("severity")]
    public required string Severity { get; init; }

    /// <summary>
    /// Human-readable description of the blocker.
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; init; }
}

/// <summary>
/// Represents the current cursor position in the execution state.
/// </summary>
public sealed record ProgressCursor
{
    /// <summary>
    /// The current phase ID.
    /// </summary>
    [JsonPropertyName("phase")]
    public string? Phase { get; init; }

    /// <summary>
    /// The current milestone ID.
    /// </summary>
    [JsonPropertyName("milestone")]
    public string? Milestone { get; init; }

    /// <summary>
    /// The current task ID.
    /// </summary>
    [JsonPropertyName("task")]
    public string? Task { get; init; }

    /// <summary>
    /// The current step ID within the task.
    /// </summary>
    [JsonPropertyName("step")]
    public string? Step { get; init; }
}

/// <summary>
/// Represents the next recommended command with arguments.
/// </summary>
public sealed record NextCommand
{
    /// <summary>
    /// The command name to execute.
    /// </summary>
    [JsonPropertyName("command")]
    public required string Command { get; init; }

    /// <summary>
    /// Optional arguments for the command.
    /// </summary>
    [JsonPropertyName("args")]
    public IReadOnlyDictionary<string, object?>? Args { get; init; }

    /// <summary>
    /// Human-readable explanation of why this command is recommended.
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

/// <summary>
/// Output contract for progress reports. Provides deterministic visibility into execution progress.
/// </summary>
public sealed record ProgressReport
{
    /// <summary>
    /// Schema version for forward compatibility.
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; init; } = "1.0";

    /// <summary>
    /// Current cursor position (phase, milestone, task, step).
    /// </summary>
    [JsonPropertyName("cursor")]
    public required ProgressCursor Cursor { get; init; }

    /// <summary>
    /// List of active blockers preventing progress.
    /// </summary>
    [JsonPropertyName("blockers")]
    public IReadOnlyList<Blocker> Blockers { get; init; } = Array.Empty<Blocker>();

    /// <summary>
    /// Next recommended command to advance execution.
    /// </summary>
    [JsonPropertyName("nextCommand")]
    public required NextCommand NextCommand { get; init; }

    /// <summary>
    /// ISO8601 timestamp of report generation.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; init; }

    /// <summary>
    /// Source run ID if active run exists.
    /// </summary>
    [JsonPropertyName("runId")]
    public string? RunId { get; init; }

    /// <summary>
    /// Indicates whether there is an active execution.
    /// </summary>
    [JsonPropertyName("hasActiveExecution")]
    public bool HasActiveExecution { get; init; }
}
