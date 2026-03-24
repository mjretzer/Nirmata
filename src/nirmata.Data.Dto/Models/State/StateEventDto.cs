namespace nirmata.Data.Dto.Models.State;

public sealed class StateEventDto
{
    public string? Type { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
    public string? Payload { get; init; }
    public IReadOnlyList<string> References { get; init; } = [];
}
