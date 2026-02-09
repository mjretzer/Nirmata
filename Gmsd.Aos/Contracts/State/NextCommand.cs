using System.Text.Json.Serialization;

namespace Gmsd.Aos.Contracts.State;

/// <summary>
/// Represents the next pending command to execute after resumption.
/// </summary>
public sealed record NextCommand
{
    /// <summary>
    /// The command name (e.g., "edit-file", "run-tests").
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// The command group/namespace.
    /// </summary>
    [JsonPropertyName("group")]
    public string? Group { get; init; }

    /// <summary>
    /// Positional arguments for the command.
    /// </summary>
    [JsonPropertyName("arguments")]
    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Named options/flags for the command.
    /// </summary>
    [JsonPropertyName("options")]
    public IReadOnlyDictionary<string, string?> Options { get; init; } = new Dictionary<string, string?>();
}
