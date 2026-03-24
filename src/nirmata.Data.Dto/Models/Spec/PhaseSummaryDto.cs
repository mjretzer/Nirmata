namespace nirmata.Data.Dto.Models.Spec;

public sealed class PhaseSummaryDto
{
    public required string Id { get; init; }
    public required string MilestoneId { get; init; }
    public required string Title { get; init; }
    public required string Status { get; init; }
    public int Order { get; init; }
    public required IReadOnlyList<string> TaskIds { get; init; }
}
