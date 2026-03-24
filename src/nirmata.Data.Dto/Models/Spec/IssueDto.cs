namespace nirmata.Data.Dto.Models.Spec;

/// <summary>
/// Represents a workspace issue record read from <c>.aos/spec/issues/ISS-####.json</c>.
/// </summary>
public sealed class IssueDto
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Status { get; init; }
    public string? Severity { get; init; }
    public string? Scope { get; init; }
    public string? Repro { get; init; }
    public string? Expected { get; init; }
    public string? Actual { get; init; }
    public IReadOnlyList<string> ImpactedFiles { get; init; } = [];
    public string? PhaseId { get; init; }
    public string? TaskId { get; init; }
    public string? MilestoneId { get; init; }
}
