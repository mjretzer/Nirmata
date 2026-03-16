using System.Text;
using System.Text.Json;

namespace nirmata.Aos.Engine.Schemas;

internal static class AosLocalSchemaRegistryLoader
{
    private static readonly Utf8EncodingNoBom Utf8NoBom = new();

    public static IReadOnlyList<LocalSchema> LoadLocalSchemas(string repositoryRootPath, bool enforceRequiredArtifactContracts = false)
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
            registry = ParseAndValidateRegistry(doc.RootElement, registryPath, enforceRequiredArtifactContracts);
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

    private static SchemaRegistryDocument ParseAndValidateRegistry(JsonElement root, string registryPath, bool enforceRequiredArtifactContracts)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Local schema registry root JSON value must be an object at '{registryPath}'.");
        }

        // Enforce additionalProperties: false (schema-registry.schema.json)
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name is "schemaVersion" or "schemas" or "artifactContracts")
            {
                continue;
            }

            throw new InvalidOperationException(
                $"Local schema registry contains unsupported property '{prop.Name}' at '{registryPath}'. " +
                "Only 'schemaVersion', 'schemas', and 'artifactContracts' are allowed."
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

        if (!root.TryGetProperty("artifactContracts", out var artifactContractsProp) || artifactContractsProp.ValueKind != JsonValueKind.Array)
        {
            if (enforceRequiredArtifactContracts)
            {
                throw new InvalidOperationException($"Local schema registry is missing required 'artifactContracts' array at '{registryPath}'.");
            }

            artifactContractsProp = default;
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

        var artifactContracts = artifactContractsProp.ValueKind == JsonValueKind.Array
            ? ParseAndValidateArtifactContracts(artifactContractsProp, registryPath)
            : Array.Empty<ArtifactContractRegistryEntry>();

        if (enforceRequiredArtifactContracts)
        {
            ValidateRequiredArtifactContracts(artifactContracts, schemas, registryPath);
        }

        return new SchemaRegistryDocument(
            SchemaVersion: schemaVersion,
            Schemas: schemas,
            ArtifactContracts: artifactContracts);
    }

    private static IReadOnlyList<ArtifactContractRegistryEntry> ParseAndValidateArtifactContracts(JsonElement artifactContractsProp, string registryPath)
    {
        var entries = new List<ArtifactContractRegistryEntry>();
        foreach (var item in artifactContractsProp.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"Local schema registry 'artifactContracts' entries must be objects at '{registryPath}'.");
            }

            var schemaId = ReadRequiredString(item, "schemaId", registryPath, "artifactContracts");
            var currentVersion = ReadRequiredInt(item, "currentVersion", registryPath, "artifactContracts");
            var supportedVersions = ReadRequiredIntArray(item, "supportedVersions", registryPath, "artifactContracts");
            var deprecatedVersions = ReadOptionalIntArray(item, "deprecatedVersions", registryPath, "artifactContracts");

            if (!supportedVersions.Contains(currentVersion))
            {
                throw new InvalidOperationException(
                    $"Local schema registry artifact contract '{schemaId}' must include currentVersion '{currentVersion}' in supportedVersions at '{registryPath}'.");
            }

            entries.Add(new ArtifactContractRegistryEntry(
                SchemaId: schemaId,
                CurrentVersion: currentVersion,
                SupportedVersions: supportedVersions,
                DeprecatedVersions: deprecatedVersions));
        }

        var duplicateSchemaIds = entries
            .GroupBy(e => e.SchemaId, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (duplicateSchemaIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"Local schema registry contains duplicate artifactContracts schemaId values at '{registryPath}': {string.Join(", ", duplicateSchemaIds)}"
            );
        }

        return entries;
    }

    private static void ValidateRequiredArtifactContracts(
        IReadOnlyList<ArtifactContractRegistryEntry> entries,
        IReadOnlyList<string> schemaFiles,
        string registryPath)
    {
        var byId = entries.ToDictionary(e => e.SchemaId, StringComparer.Ordinal);
        var schemaFileSet = schemaFiles.ToHashSet(StringComparer.Ordinal);

        foreach (var required in ArtifactContractSchemaCatalog.RequiredContracts)
        {
            if (!byId.TryGetValue(required.SchemaId, out var actual))
            {
                throw new InvalidOperationException(
                    $"Local schema registry is missing required artifactContracts entry for schemaId '{required.SchemaId}' at '{registryPath}'.");
            }

            if (actual.CurrentVersion != required.CurrentVersion)
            {
                throw new InvalidOperationException(
                    $"Local schema registry artifactContracts entry '{required.SchemaId}' has currentVersion '{actual.CurrentVersion}', expected '{required.CurrentVersion}' at '{registryPath}'.");
            }

            foreach (var requiredVersion in required.SupportedVersions)
            {
                if (!actual.SupportedVersions.Contains(requiredVersion))
                {
                    throw new InvalidOperationException(
                        $"Local schema registry artifactContracts entry '{required.SchemaId}' is missing supportedVersion '{requiredVersion}' at '{registryPath}'.");
                }
            }

            if (!schemaFileSet.Contains(required.SchemaFileName))
            {
                throw new InvalidOperationException(
                    $"Local schema registry is missing required schema file '{required.SchemaFileName}' for schemaId '{required.SchemaId}' at '{registryPath}'.");
            }
        }
    }

    private static string ReadRequiredString(JsonElement obj, string propertyName, string registryPath, string section)
    {
        if (!obj.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Local schema registry '{section}' entries must include string '{propertyName}' at '{registryPath}'.");
        }

        var value = prop.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Local schema registry '{section}' entries must include non-empty string '{propertyName}' at '{registryPath}'.");
        }

        return value;
    }

    private static int ReadRequiredInt(JsonElement obj, string propertyName, string registryPath, string section)
    {
        if (!obj.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Number || !prop.TryGetInt32(out var value))
        {
            throw new InvalidOperationException($"Local schema registry '{section}' entries must include integer '{propertyName}' at '{registryPath}'.");
        }

        if (value < 1)
        {
            throw new InvalidOperationException($"Local schema registry '{section}' entries must include positive integer '{propertyName}' at '{registryPath}'.");
        }

        return value;
    }

    private static IReadOnlyList<int> ReadRequiredIntArray(JsonElement obj, string propertyName, string registryPath, string section)
    {
        if (!obj.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Local schema registry '{section}' entries must include '{propertyName}' array at '{registryPath}'.");
        }

        var values = ReadIntArrayCore(prop, propertyName, registryPath, section);
        if (values.Count == 0)
        {
            throw new InvalidOperationException($"Local schema registry '{section}' entries must include non-empty '{propertyName}' array at '{registryPath}'.");
        }

        return values;
    }

    private static IReadOnlyList<int> ReadOptionalIntArray(JsonElement obj, string propertyName, string registryPath, string section)
    {
        if (!obj.TryGetProperty(propertyName, out var prop))
        {
            return Array.Empty<int>();
        }

        if (prop.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Local schema registry '{section}' entries must include '{propertyName}' array at '{registryPath}'.");
        }

        return ReadIntArrayCore(prop, propertyName, registryPath, section);
    }

    private static IReadOnlyList<int> ReadIntArrayCore(JsonElement prop, string propertyName, string registryPath, string section)
    {
        var values = new List<int>();
        foreach (var item in prop.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out var value) || value < 1)
            {
                throw new InvalidOperationException($"Local schema registry '{section}' entries '{propertyName}' values must be positive integers at '{registryPath}'.");
            }

            values.Add(value);
        }

        return values
            .Distinct()
            .Order()
            .ToArray();
    }

    internal sealed record LocalSchema(string Id, string FileName, string FullPath, string Json);

    internal sealed record ArtifactContractRegistryEntry(
        string SchemaId,
        int CurrentVersion,
        IReadOnlyList<int> SupportedVersions,
        IReadOnlyList<int> DeprecatedVersions);

    private sealed record SchemaRegistryDocument(
        int SchemaVersion,
        IReadOnlyList<string> Schemas,
        IReadOnlyList<ArtifactContractRegistryEntry> ArtifactContracts);

    private sealed class Utf8EncodingNoBom
    {
        public Encoding Instance { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }
}

