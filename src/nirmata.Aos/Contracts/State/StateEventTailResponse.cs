using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace nirmata.Aos.Contracts.State;

/// <summary>
/// Tail/filter response containing events in stable file order.
/// </summary>
public sealed record StateEventTailResponse
{
    [JsonPropertyName("items")]
    public IReadOnlyList<StateEventEntry> Items { get; init; } = Array.Empty<StateEventEntry>();
}

