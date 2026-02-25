using System.Text.Json.Serialization;

namespace Gmsd.Aos.Contracts.State;

/// <summary>
/// Represents the scope constraints for an execution context.
/// </summary>
public sealed record ScopeConstraints
{
    /// <summary>
    /// List of file paths that are whitelisted for editing.
    /// </summary>
    [JsonPropertyName("fileWhitelist")]
    public IReadOnlyList<string> FileWhitelist { get; init; } = Array.Empty<string>();

    /// <summary>
    /// List of directory paths that are whitelisted for operations.
    /// </summary>
    [JsonPropertyName("directoryWhitelist")]
    public IReadOnlyList<string> DirectoryWhitelist { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Glob patterns for file exclusions.
    /// </summary>
    [JsonPropertyName("excludePatterns")]
    public IReadOnlyList<string> ExcludePatterns { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Edit restrictions (e.g., "read-only", "append-only", "full").
    /// </summary>
    [JsonPropertyName("editRestrictions")]
    public string? EditRestrictions { get; init; }
}
