using System.Text.Json.Serialization;

namespace nirmata.Data.Dto.Models.Chat;

/// <summary>
/// A one-click quick action surfaced in the chat panel.
/// Activating the action submits <see cref="Command"/> through the chat endpoint.
/// </summary>
public sealed class QuickActionDto
{
    /// <summary>Display label for the action button.</summary>
    [JsonPropertyName("label")]
    public required string Label { get; init; }

    /// <summary>The <c>aos</c> command submitted when the action is activated.</summary>
    [JsonPropertyName("command")]
    public required string Command { get; init; }

    /// <summary>
    /// Optional icon hint for the frontend (e.g. <c>play</c>, <c>check</c>, <c>refresh</c>).
    /// </summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; init; }
}
