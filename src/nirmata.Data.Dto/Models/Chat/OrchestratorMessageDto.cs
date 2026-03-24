using System.Text.Json.Serialization;
using nirmata.Data.Dto.Models.OrchestratorGate;

namespace nirmata.Data.Dto.Models.Chat;

/// <summary>
/// A single message in the workspace chat thread, aligned to the internal
/// <c>OrchestratorMessage</c> contract.
/// Returned inside <see cref="ChatSnapshotDto"/> and as the body of
/// <c>POST /v1/workspaces/{workspaceId}/chat</c>.
/// </summary>
public sealed class OrchestratorMessageDto
{
    /// <summary>
    /// Message role: <c>user</c>, <c>assistant</c>, <c>system</c>, or <c>result</c>.
    /// </summary>
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    /// <summary>Primary text content of the message.</summary>
    [JsonPropertyName("content")]
    public required string Content { get; init; }

    /// <summary>
    /// Gate evaluation snapshot at the time this message was produced.
    /// <see langword="null"/> for user-role messages.
    /// </summary>
    [JsonPropertyName("gate")]
    public OrchestratorGateDto? Gate { get; init; }

    /// <summary>
    /// Artifact references produced or referenced during this turn.
    /// Empty list when the turn did not touch workspace artifacts.
    /// </summary>
    [JsonPropertyName("artifacts")]
    public IReadOnlyList<string> Artifacts { get; init; } = [];

    /// <summary>
    /// Ordered timeline snapshot captured at the end of this turn.
    /// <see langword="null"/> when no timeline data is available.
    /// </summary>
    [JsonPropertyName("timeline")]
    public OrchestratorTimelineDto? Timeline { get; init; }

    /// <summary>
    /// The next recommended <c>aos</c> command surfaced by the orchestrator, or
    /// <see langword="null"/> when the workspace is in a clean state.
    /// </summary>
    [JsonPropertyName("nextCommand")]
    public string? NextCommand { get; init; }

    /// <summary>
    /// Identifier of the agent run that produced this message (e.g. <c>RUN-…</c>),
    /// or <see langword="null"/> when no run was created for the turn.
    /// </summary>
    [JsonPropertyName("runId")]
    public string? RunId { get; init; }

    /// <summary>
    /// Log lines captured during the run, or an empty list when no logs are available.
    /// </summary>
    [JsonPropertyName("logs")]
    public IReadOnlyList<string> Logs { get; init; } = [];

    /// <summary>UTC timestamp when this message was produced.</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Identifier of the agent that produced this message (e.g. <c>orchestrator</c>,
    /// <c>planner</c>), or <see langword="null"/> for user-role messages.
    /// </summary>
    [JsonPropertyName("agentId")]
    public string? AgentId { get; init; }
}
