namespace nirmata.Data.Dto.Models.Spec;

public sealed class TaskSummaryDto
{
    public required string Id { get; init; }
    public required string PhaseId { get; init; }
    public required string MilestoneId { get; init; }
    public required string Title { get; init; }
    public required string Status { get; init; }
}
