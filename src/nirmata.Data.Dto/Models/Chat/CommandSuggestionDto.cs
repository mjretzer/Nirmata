using System.Text.Json.Serialization;

namespace nirmata.Data.Dto.Models.Chat;

/// <summary>
/// A single autocomplete command suggestion returned in a <see cref="ChatSnapshotDto"/>.
/// </summary>
public sealed class CommandSuggestionDto
{
    /// <summary>The suggested command text (e.g. <c>aos status</c>).</summary>
    [JsonPropertyName("command")]
    public required string Command { get; init; }

    /// <summary>Short description shown alongside the suggestion.</summary>
    [JsonPropertyName("description")]
    public required string Description { get; init; }
}
