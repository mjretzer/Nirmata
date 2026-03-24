namespace nirmata.Data.Dto.Models.State;

public sealed class CheckpointSummaryDto
{
    /// <summary>Checkpoint filename timestamp, e.g. <c>2026-01-13T021500Z</c>.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The workflow cursor captured in this checkpoint.</summary>
    public StatePositionDto? Position { get; init; }

    /// <summary>When the checkpoint was written.</summary>
    public DateTimeOffset? Timestamp { get; init; }
}
