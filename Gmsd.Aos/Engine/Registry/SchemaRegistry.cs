using System.Text.Json;
using Gmsd.Aos.Engine.Schemas;
using Gmsd.Aos.Public;

namespace Gmsd.Aos.Engine.Registry;

/// <summary>
/// Implementation of <see cref="ISchemaRegistry"/> that provides access to embedded and local schemas.
/// </summary>
internal sealed class SchemaRegistry : ISchemaRegistry
{
    private readonly IWorkspace _workspace;
    private readonly IReadOnlyDictionary<string, AosEmbeddedSchemaRegistryLoader.EmbeddedSchema> _embeddedSchemasById;
    private readonly IReadOnlyDictionary<string, AosLocalSchemaRegistryLoader.LocalSchema> _localSchemasById;
    private readonly Dictionary<string, JsonDocument> _schemaCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaRegistry"/> class.
    /// </summary>
    /// <param name="workspace">The workspace to use for locating local schemas.</param>
    public SchemaRegistry(IWorkspace workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _schemaCache = new Dictionary<string, JsonDocument>(StringComparer.Ordinal);

        // Load embedded schemas at startup
        var embeddedRegistry = AosEmbeddedSchemaRegistryLoader.LoadEmbeddedSchemaRegistry();
        _embeddedSchemasById = embeddedRegistry.ById;

        // Load local schemas at startup (if available)
        var localSchemas = LoadLocalSchemasSafe(workspace.RepositoryRootPath);
        _localSchemasById = localSchemas.ToDictionary(s => s.Id, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public JsonDocument GetSchema(string schemaId)
    {
        if (string.IsNullOrWhiteSpace(schemaId))
        {
            throw new ArgumentException("Schema ID cannot be null or whitespace.", nameof(schemaId));
        }

        if (TryGetSchema(schemaId, out var schema) && schema is not null)
        {
            return schema;
        }

        throw new KeyNotFoundException($"Schema with $id '{schemaId}' not found in registry.");
    }

    /// <inheritdoc />
    public bool TryGetSchema(string schemaId, out JsonDocument? schema)
    {
        if (string.IsNullOrWhiteSpace(schemaId))
        {
            schema = null;
            return false;
        }

        // Check cache first
        if (_schemaCache.TryGetValue(schemaId, out var cachedSchema))
        {
            schema = cachedSchema;
            return true;
        }

        // Local schemas take precedence over embedded schemas
        if (_localSchemasById.TryGetValue(schemaId, out var localSchema))
        {
            schema = ParseAndCacheSchema(schemaId, localSchema.Json);
            return true;
        }

        // Fall back to embedded schemas
        if (_embeddedSchemasById.TryGetValue(schemaId, out var embeddedSchema))
        {
            schema = ParseAndCacheSchema(schemaId, embeddedSchema.Json);
            return true;
        }

        schema = null;
        return false;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ListSchemaIds()
    {
        // Union of local and embedded schema IDs, with local taking precedence
        var ids = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var id in _embeddedSchemasById.Keys)
        {
            ids.Add(id);
        }

        foreach (var id in _localSchemasById.Keys)
        {
            ids.Add(id);
        }

        return ids.ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ListEmbeddedSchemaIds()
    {
        return _embeddedSchemasById.Keys.Order(StringComparer.Ordinal).ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ListLocalSchemaIds()
    {
        return _localSchemasById.Keys.Order(StringComparer.Ordinal).ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public bool HasSchema(string schemaId)
    {
        if (string.IsNullOrWhiteSpace(schemaId))
        {
            return false;
        }

        return _localSchemasById.ContainsKey(schemaId) || _embeddedSchemasById.ContainsKey(schemaId);
    }

    private JsonDocument ParseAndCacheSchema(string schemaId, string json)
    {
        if (_schemaCache.TryGetValue(schemaId, out var cached))
        {
            return cached;
        }

        var options = new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow
        };

        var doc = JsonDocument.Parse(json, options);
        _schemaCache[schemaId] = doc;
        return doc;
    }

    private static IReadOnlyList<AosLocalSchemaRegistryLoader.LocalSchema> LoadLocalSchemasSafe(string repositoryRootPath)
    {
        try
        {
            return AosLocalSchemaRegistryLoader.LoadLocalSchemas(repositoryRootPath);
        }
        catch
        {
            // Local schema pack may not exist (e.g., before 'aos init')
            return Array.Empty<AosLocalSchemaRegistryLoader.LocalSchema>();
        }
    }
}
