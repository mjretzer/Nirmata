namespace Gmsd.Aos.Templates.Prompts;

/// <summary>
/// Represents a loaded prompt template with its metadata.
/// </summary>
public sealed record PromptTemplate
{
    /// <summary>
    /// Unique identifier for the template (e.g., "planning.task-breakdown.v1").
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The template content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Optional metadata for the template.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Template version parsed from the ID.
    /// </summary>
    public string? Version => Metadata.GetValueOrDefault("version");

    /// <summary>
    /// Template purpose/domain parsed from the ID.
    /// </summary>
    public string? Purpose
    {
        get
        {
            var parts = Id.Split('.');
            return parts.Length >= 2 ? parts[1] : null;
        }
    }
}
