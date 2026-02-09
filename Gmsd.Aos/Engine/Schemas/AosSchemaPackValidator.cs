using System.Text.Json;

namespace Gmsd.Aos.Engine.Schemas;

internal static class AosSchemaPackValidator
{
    private const string ExpectedSchemaUri = "https://json-schema.org/draft/2020-12/schema";

    public static AosSchemaPackValidationReport ValidateEmbeddedSchemas(
        IReadOnlyList<AosEmbeddedSchemaRegistryLoader.EmbeddedSchema> schemas)
    {
        if (schemas is null) throw new ArgumentNullException(nameof(schemas));

        return ValidateSchemasCore(
            schemas.Select(static s => new SchemaDocument(s.FileName, s.Json)).ToArray()
        );
    }

    public static AosSchemaPackValidationReport ValidateLocalSchemas(
        IReadOnlyList<AosLocalSchemaRegistryLoader.LocalSchema> schemas)
    {
        if (schemas is null) throw new ArgumentNullException(nameof(schemas));

        return ValidateSchemasCore(
            schemas.Select(static s => new SchemaDocument(s.FileName, s.Json)).ToArray()
        );
    }

    private static AosSchemaPackValidationReport ValidateSchemasCore(IReadOnlyList<SchemaDocument> schemas)
    {
        var issues = new List<AosSchemaPackValidationIssue>();
        var schemaIds = new List<(string FileName, string? Id)>(schemas.Count);

        foreach (var schema in schemas)
        {
            try
            {
                using var doc = JsonDocument.Parse(schema.Json);

                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(new AosSchemaPackValidationIssue(schema.FileName, "Root JSON value must be an object."));
                    schemaIds.Add((schema.FileName, null));
                    continue;
                }

                var root = doc.RootElement;

                if (!TryGetStringProperty(root, "$schema", out var schemaUri))
                {
                    issues.Add(new AosSchemaPackValidationIssue(schema.FileName, "Missing required '$schema' string."));
                }
                else if (!string.Equals(schemaUri, ExpectedSchemaUri, StringComparison.Ordinal))
                {
                    issues.Add(new AosSchemaPackValidationIssue(
                        schema.FileName,
                        $"Unsupported '$schema' value '{schemaUri}'. Expected '{ExpectedSchemaUri}'."
                    ));
                }

                string? schemaId = null;
                if (!TryGetStringProperty(root, "$id", out var id))
                {
                    issues.Add(new AosSchemaPackValidationIssue(schema.FileName, "Missing required '$id' string."));
                }
                else
                {
                    schemaId = id;
                }

                if (!TryGetStringProperty(root, "type", out _))
                {
                    issues.Add(new AosSchemaPackValidationIssue(schema.FileName, "Missing required 'type' string."));
                }

                schemaIds.Add((schema.FileName, schemaId));
            }
            catch (JsonException ex)
            {
                issues.Add(new AosSchemaPackValidationIssue(schema.FileName, $"Invalid JSON: {ex.Message}"));
                schemaIds.Add((schema.FileName, null));
            }
        }

        var duplicates = schemaIds
            .Where(static x => x.Id is not null)
            .GroupBy(static x => x.Id!, StringComparer.Ordinal)
            .Where(static g => g.Count() > 1)
            .Select(static g => new
            {
                Id = g.Key,
                Files = g.Select(static x => x.FileName).Order(StringComparer.Ordinal).ToArray()
            })
            .OrderBy(static x => x.Id, StringComparer.Ordinal)
            .ToArray();

        foreach (var dup in duplicates)
        {
            var message =
                $"Duplicate '$id' value '{dup.Id}' appears in multiple schema files: {string.Join(", ", dup.Files)}.";

            foreach (var file in dup.Files)
            {
                issues.Add(new AosSchemaPackValidationIssue(file, message));
            }
        }

        return new AosSchemaPackValidationReport(schemas.Count, issues);
    }

    private static bool TryGetStringProperty(JsonElement obj, string propertyName, out string value)
    {
        if (!obj.TryGetProperty(propertyName, out var prop))
        {
            value = "";
            return false;
        }

        if (prop.ValueKind != JsonValueKind.String)
        {
            value = "";
            return false;
        }

        value = prop.GetString() ?? "";
        return !string.IsNullOrWhiteSpace(value);
    }

    private sealed record SchemaDocument(string FileName, string Json);
}

internal sealed record AosSchemaPackValidationReport(
    int SchemaCount,
    IReadOnlyList<AosSchemaPackValidationIssue> Issues
);

internal sealed record AosSchemaPackValidationIssue(string SchemaFileName, string Message);

