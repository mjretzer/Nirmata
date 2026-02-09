using System.Text;
using System.Text.Json;

namespace Gmsd.Aos.Engine.Schemas;

internal static class AosLocalSchemaRegistryLoader
{
    private static readonly Utf8EncodingNoBom Utf8NoBom = new();

    public static IReadOnlyList<LocalSchema> LoadLocalSchemas(string repositoryRootPath)
    {
        if (repositoryRootPath is null) throw new ArgumentNullException(nameof(repositoryRootPath));

        var schemasRootPath = Path.Combine(repositoryRootPath, ".aos", "schemas");
        var registryPath = Path.Combine(schemasRootPath, "registry.json");

        if (!File.Exists(registryPath))
        {
            throw new InvalidOperationException(
                $"Local schema registry not found at '{registryPath}'. " +
                "Run 'aos init' to seed the local schema pack under '.aos/schemas/'."
            );
        }

        SchemaRegistryDocument registry;
        try
        {
            // Treat registry.json as UTF-8 text for deterministic behavior.
            // (JsonDocument handles UTF-8 bytes, but we want stable decoding semantics.)
            var json = File.ReadAllText(registryPath, Utf8NoBom.Instance);
            using var doc = JsonDocument.Parse(json);
            registry = ParseAndValidateRegistry(doc.RootElement, registryPath);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Local schema registry is not valid JSON at '{registryPath}': {ex.Message}");
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Failed to read local schema registry at '{registryPath}': {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException($"Failed to read local schema registry at '{registryPath}': {ex.Message}");
        }

        var schemas = new List<LocalSchema>(registry.Schemas.Count);

        foreach (var fileName in registry.Schemas)
        {
            var fullPath = Path.Combine(schemasRootPath, fileName);
            if (!File.Exists(fullPath))
            {
                throw new InvalidOperationException(
                    $"Local schema registry references missing schema file '{fileName}'. Expected file at '{fullPath}'."
                );
            }

            string json;
            try
            {
                json = File.ReadAllText(fullPath, Utf8NoBom.Instance);
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException($"Failed to read local schema file '{fileName}' at '{fullPath}': {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException($"Failed to read local schema file '{fileName}' at '{fullPath}': {ex.Message}");
            }

            // Normalize line endings for deterministic behavior across platforms.
            json = json.Replace("\r\n", "\n");

            schemas.Add(new LocalSchema(
                Id: AosSchemaFileNamePolicy.ToSchemaId(fileName),
                FileName: fileName,
                FullPath: fullPath,
                Json: json
            ));
        }

        return schemas;
    }

    private static SchemaRegistryDocument ParseAndValidateRegistry(JsonElement root, string registryPath)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Local schema registry root JSON value must be an object at '{registryPath}'.");
        }

        // Enforce additionalProperties: false (schema-registry.schema.json)
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name is "schemaVersion" or "schemas")
            {
                continue;
            }

            throw new InvalidOperationException(
                $"Local schema registry contains unsupported property '{prop.Name}' at '{registryPath}'. " +
                "Only 'schemaVersion' and 'schemas' are allowed."
            );
        }

        if (!root.TryGetProperty("schemaVersion", out var schemaVersionProp) ||
            schemaVersionProp.ValueKind != JsonValueKind.Number ||
            !schemaVersionProp.TryGetInt32(out var schemaVersion))
        {
            throw new InvalidOperationException($"Local schema registry is missing required 'schemaVersion' integer at '{registryPath}'.");
        }

        if (schemaVersion != 1)
        {
            throw new InvalidOperationException($"Unsupported local schema registry schemaVersion '{schemaVersion}' at '{registryPath}'. Expected 1.");
        }

        if (!root.TryGetProperty("schemas", out var schemasProp) || schemasProp.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Local schema registry is missing required 'schemas' array at '{registryPath}'.");
        }

        var schemas = new List<string>();
        foreach (var item in schemasProp.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException($"Local schema registry 'schemas' items must be strings at '{registryPath}'.");
            }

            var fileName = item.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new InvalidOperationException($"Local schema registry 'schemas' items must be non-empty strings at '{registryPath}'.");
            }

            // Filenames only; no directory separators.
            if (fileName.Contains('/') || fileName.Contains('\\'))
            {
                throw new InvalidOperationException(
                    $"Local schema registry 'schemas' entry '{fileName}' is not a filename-only entry at '{registryPath}'."
                );
            }

            AosSchemaFileNamePolicy.EnsureCanonicalSchemaFileName(fileName, $"registry '{registryPath}'");

            schemas.Add(fileName);
        }

        if (schemas.Count == 0)
        {
            throw new InvalidOperationException(
                $"Local schema registry 'schemas' must contain at least one schema file at '{registryPath}'."
            );
        }

        var duplicates = schemas
            .GroupBy(s => s, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                $"Local schema registry contains duplicate schema entries at '{registryPath}': {string.Join(", ", duplicates)}"
            );
        }

        return new SchemaRegistryDocument(SchemaVersion: schemaVersion, Schemas: schemas);
    }

    internal sealed record LocalSchema(string Id, string FileName, string FullPath, string Json);

    private sealed record SchemaRegistryDocument(int SchemaVersion, IReadOnlyList<string> Schemas);

    private sealed class Utf8EncodingNoBom
    {
        public Encoding Instance { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }
}

