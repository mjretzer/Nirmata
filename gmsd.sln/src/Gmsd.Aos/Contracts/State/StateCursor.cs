using System.Text.Json.Serialization;

namespace Gmsd.Aos.Contracts.State;

/// <summary>
/// Stable cursor contract. This matches the state snapshot <c>cursor</c> object and includes:
/// - cursor v2 fields (milestone/phase/task/step ids + statuses)
/// - legacy cursor reference fields (<c>kind</c>/<c>id</c>) for temporary compatibility
/// </summary>
public sealed record StateCursor
{
    [JsonPropertyName("milestoneId")]
    public string? MilestoneId { get; init; }

    [JsonPropertyName("phaseId")]
    public string? PhaseId { get; init; }

    [JsonPropertyName("taskId")]
    public string? TaskId { get; init; }

    [JsonPropertyName("stepId")]
    public string? StepId { get; init; }

    [JsonPropertyName("milestoneStatus")]
    public string? MilestoneStatus { get; init; }

    [JsonPropertyName("phaseStatus")]
    public string? PhaseStatus { get; init; }

    [JsonPropertyName("taskStatus")]
    public string? TaskStatus { get; init; }

    [JsonPropertyName("stepStatus")]
    public string? StepStatus { get; init; }

    // Legacy cursor reference (deprecated for operational cursoring)
    [JsonPropertyName("kind")]
    public string? Kind { get; init; }

    [JsonPropertyName("id")]
    public string? Id { get; init; }
}

