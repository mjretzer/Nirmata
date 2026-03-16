using System.Text.Json.Serialization;

namespace nirmata.Aos.Contracts.State;

/// <summary>
/// Tail/filter request options for reading an ordered slice of <c>.aos/state/events.ndjson</c>.
/// </summary>
public sealed record StateEventTailRequest
{
    /// <summary>
    /// Exclusive 1-based line cursor; all events at or before this line are skipped.
    /// </summary>
    [JsonPropertyName("sinceLine")]
    public int SinceLine { get; init; }

    /// <summary>
    /// Optional cap on returned items while preserving file order.
    /// </summary>
    [JsonPropertyName("maxItems")]
    public int? MaxItems { get; init; }

    /// <summary>
    /// Optional filter for events whose <c>eventType</c> equals this value.
    /// </summary>
    [JsonPropertyName("eventType")]
    public string? EventType { get; init; }

    /// <summary>
    /// Optional legacy filter for events whose <c>kind</c> equals this value.
    /// </summary>
    [JsonPropertyName("kind")]
    public string? Kind { get; init; }
}

