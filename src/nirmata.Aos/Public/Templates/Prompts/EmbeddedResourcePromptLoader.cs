using System.Reflection;
using Microsoft.Extensions.Logging;

namespace nirmata.Aos.Public.Templates.Prompts;

/// <summary>
/// Loads prompt templates from embedded resources.
/// </summary>
public sealed class EmbeddedResourcePromptLoader : IPromptTemplateLoader
{
    private readonly ILogger<EmbeddedResourcePromptLoader>? _logger;
    private readonly Dictionary<string, PromptTemplate> _templates;
    private readonly string _resourcePrefix;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedResourcePromptLoader"/> class.
    /// </summary>
    public EmbeddedResourcePromptLoader(
        Assembly assembly,
        string resourcePrefix = "nirmata.Aos.Resources.Prompts",
        ILogger<EmbeddedResourcePromptLoader>? logger = null)
    {
        _logger = logger;
        _resourcePrefix = resourcePrefix;
        _templates = LoadTemplates(assembly);
    }

    /// <inheritdoc />
    public PromptTemplate? GetById(string id) =>
        _templates.TryGetValue(id, out var template) ? template : null;

    /// <inheritdoc />
    public bool Exists(string id) => _templates.ContainsKey(id);

    private Dictionary<string, PromptTemplate> LoadTemplates(Assembly assembly)
    {
        var templates = new Dictionary<string, PromptTemplate>(StringComparer.OrdinalIgnoreCase);
        var resourceNames = assembly.GetManifestResourceNames();

        foreach (var resourceName in resourceNames)
        {
            if (!resourceName.StartsWith(_resourcePrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var fileName = resourceName.Substring(_resourcePrefix.Length).TrimStart('.');
            var id = ParseTemplateId(fileName);

            if (string.IsNullOrEmpty(id))
                continue;

            try
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                    continue;

                using var reader = new StreamReader(stream);
                var content = reader.ReadToEnd();

                templates[id] = new PromptTemplate
                {
                    Id = id,
                    Content = content,
                    Metadata = ExtractMetadata(content, fileName)
                };

                _logger?.LogDebug("Loaded prompt template: {TemplateId}", id);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load prompt template: {ResourceName}", resourceName);
            }
        }

        return templates;
    }

    private static string? ParseTemplateId(string fileName)
    {
        // Convert "planning.task-breakdown.v1.prompt.txt" to "planning.task-breakdown.v1"
        var withoutExtension = fileName;
        if (fileName.EndsWith(".prompt.txt", StringComparison.OrdinalIgnoreCase))
            withoutExtension = fileName[..^11];
        else if (fileName.EndsWith(".prompt.md", StringComparison.OrdinalIgnoreCase))
            withoutExtension = fileName[..^10];
        else if (fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            withoutExtension = fileName[..^4];
        else if (fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            withoutExtension = fileName[..^3];

        return string.IsNullOrEmpty(withoutExtension) ? null : withoutExtension.Replace('_', '.');
    }

    private static Dictionary<string, string> ExtractMetadata(string content, string fileName)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Extract version from filename (e.g., "v1" from "planning.task-breakdown.v1")
        var parts = fileName.Split('.');
        if (parts.Length >= 2 && parts[^2].StartsWith('v'))
        {
            metadata["version"] = parts[^2];
        }

        // Parse YAML frontmatter if present
        if (content.StartsWith("---", StringComparison.Ordinal))
        {
            var endIndex = content.IndexOf("---", 3, StringComparison.Ordinal);
            if (endIndex > 0)
            {
                var frontmatter = content[3..endIndex].Trim();
                foreach (var line in frontmatter.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var colonIndex = line.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        var key = line[..colonIndex].Trim();
                        var value = line[(colonIndex + 1)..].Trim();
                        metadata[key] = value;
                    }
                }
            }
        }

        return metadata;
    }
}
