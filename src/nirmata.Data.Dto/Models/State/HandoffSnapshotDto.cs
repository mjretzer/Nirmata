namespace nirmata.Data.Dto.Models.State;

public sealed class HandoffSnapshotDto
{
    public StatePositionDto? Cursor { get; init; }
    public string? InFlightTask { get; init; }
    public int? InFlightStep { get; init; }
    public IReadOnlyList<string> AllowedScope { get; init; } = [];
    public bool PendingVerification { get; init; }
    public string? NextCommand { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
}
