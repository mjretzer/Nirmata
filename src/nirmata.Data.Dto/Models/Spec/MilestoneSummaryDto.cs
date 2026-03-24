namespace nirmata.Data.Dto.Models.Spec;

public sealed class MilestoneSummaryDto
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Status { get; init; }
    public required IReadOnlyList<string> PhaseIds { get; init; }
}
