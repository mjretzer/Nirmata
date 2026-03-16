using System.Reflection;
using System.Text;
using System.Text.Json;

namespace nirmata.Aos.Engine.Schemas;

internal static class AosEmbeddedSchemaRegistryLoader
{
    public static IReadOnlyList<EmbeddedSchema> LoadEmbeddedSchemas(Assembly? assembly = null)
    {
        assembly ??= typeof(AosEmbeddedSchemaRegistryLoader).Assembly;

        // NOTE: Manifest resource names use the project's RootNamespace by default, not the AssemblyName.
        // Since our AssemblyName is configured as `aos`, but the RootNamespace remains `nirmata.Aos`,
        // discover resources by a stable marker segment instead of assuming a prefix.
        const string marker = ".Resources.Schemas.";

        var resourceNames = assembly
            .GetManifestResourceNames()
            .Where(n =>
                n.Contains(marker, StringComparison.Ordinal) &&
                n.EndsWith(AosSchemaFileNamePolicy.SchemaSuffix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (resourceNames.Length == 0)
        {
            throw new InvalidOperationException(
                $"No embedded schema resources found. Expected resources under '*{marker}*{AosSchemaFileNamePolicy.SchemaSuffix}'. " +
                "Ensure the schema files are included as EmbeddedResource items."
            );
        }

        var result = new List<EmbeddedSchema>(resourceNames.Length);

        foreach (var resourceName in resourceNames)
        {
            var markerIndex = resourceName.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                // Should be impossible due to filtering above.
                throw new InvalidOperationException($"Embedded schema resource '{resourceName}' is missing marker segment '{marker}'.");
            }

            var fileName = resourceName[(markerIndex + marker.Length)..];
            AosSchemaFileNamePolicy.EnsureCanonicalSchemaFileName(fileName, $"resource '{resourceName}'");

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                throw new InvalidOperationException($"Embedded schema resource '{resourceName}' could not be opened.");
            }

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
            var json = reader.ReadToEnd();

            // Keep behavior deterministic across platforms by normalizing line endings.
            json = json.Replace("\r\n", "\n");

            var schemaId = ReadAndValidateSchemaId(json, resourceName, fileName);

            result.Add(new EmbeddedSchema(
                Id: schemaId,
                FileName: fileName,
                ResourceName: resourceName,
                Json: json
            ));
        }

        // Deterministic ordering independent of manifest ordering.
        result.Sort(static (a, b) =>
        {
            var byId = StringComparer.Ordinal.Compare(a.Id, b.Id);
            if (byId != 0) return byId;
            return StringComparer.Ordinal.Compare(a.FileName, b.FileName);
        });

        // Enforce unique schema identity by $id.
        var duplicates = result
            .GroupBy(static s => s.Id, StringComparer.Ordinal)
            .Where(static g => g.Count() > 1)
            .Select(static g => new
            {
                Id = g.Key,
                Files = g.Select(static s => s.FileName).Order(StringComparer.Ordinal).ToArray()
            })
            .OrderBy(static x => x.Id, StringComparer.Ordinal)
            .ToArray();

        if (duplicates.Length > 0)
        {
            var details = string.Join(
                "; ",
                duplicates.Select(static d => $"'{d.Id}' in [{string.Join(", ", d.Files)}]")
            );

            throw new InvalidOperationException($"Embedded schema pack contains duplicate '$id' values: {details}");
        }

        return result;
    }

    public static EmbeddedSchemaRegistry LoadEmbeddedSchemaRegistry(Assembly? assembly = null)
    {
        var schemas = LoadEmbeddedSchemas(assembly);

        // Deterministic $id index (SortedDictionary enumerates in key order).
        var byId = new SortedDictionary<string, EmbeddedSchema>(StringComparer.Ordinal);
        foreach (var schema in schemas)
        {
            byId.Add(schema.Id, schema);
        }

        return new EmbeddedSchemaRegistry(schemas, byId);
    }

    private static string ReadAndValidateSchemaId(string json, string resourceName, string fileName)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    $"Embedded schema '{fileName}' (resource '{resourceName}') root JSON value must be an object."
                );
            }

            var root = doc.RootElement;
            if (!root.TryGetProperty("$id", out var idProp) || idProp.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    $"Embedded schema '{fileName}' (resource '{resourceName}') is missing required '$id' string."
                );
            }

            var id = idProp.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidOperationException(
                    $"Embedded schema '{fileName}' (resource '{resourceName}') has an empty '$id' value."
                );
            }

            return id;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Embedded schema '{fileName}' (resource '{resourceName}') is not valid JSON: {ex.Message}"
            );
        }
    }

    internal sealed record EmbeddedSchema(string Id, string FileName, string ResourceName, string Json);

    internal sealed record EmbeddedSchemaRegistry(
        IReadOnlyList<EmbeddedSchema> Schemas,
        IReadOnlyDictionary<string, EmbeddedSchema> ById);
}

