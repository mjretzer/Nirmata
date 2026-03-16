using System.Text.Json;
using System.Text.Json.Serialization;

namespace nirmata.Aos.Contracts.State;

/// <summary>
/// Represents a single parsed NDJSON line from <c>.aos/state/events.ndjson</c>.
/// </summary>
public sealed record StateEventEntry
{
    /// <summary>
    /// The 1-based line number in the source file.
    /// </summary>
    [JsonPropertyName("lineNumber")]
    public int LineNumber { get; init; }

    /// <summary>
    /// The parsed JSON object payload for the event line.
    /// </summary>
    [JsonPropertyName("payload")]
    public JsonElement Payload { get; init; }
}

