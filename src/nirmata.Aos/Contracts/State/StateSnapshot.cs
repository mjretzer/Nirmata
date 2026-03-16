using System.Text.Json.Serialization;

namespace nirmata.Aos.Contracts.State;

/// <summary>
/// Stable contract representing <c>.aos/state/state.json</c>.
/// </summary>
public sealed record StateSnapshot
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName("cursor")]
    public StateCursor Cursor { get; init; } = new();
}

