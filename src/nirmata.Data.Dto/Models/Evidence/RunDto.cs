namespace nirmata.Data.Dto.Models.Evidence;

public sealed class RunSummaryDto
{
    public string Id { get; init; } = string.Empty;
    public string? TaskId { get; init; }
    public string? Status { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
}

public sealed class RunDetailDto
{
    public string Id { get; init; } = string.Empty;
    public string? TaskId { get; init; }
    public string? Status { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
    public IReadOnlyList<string> Commands { get; init; } = [];
    public IReadOnlyList<string> LogFiles { get; init; } = [];
    public IReadOnlyList<string> Artifacts { get; init; } = [];
}
