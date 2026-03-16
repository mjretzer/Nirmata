using System.Text;
using System.Text.Json;
using nirmata.Aos.Engine.Schemas;
using Json.Schema;

namespace nirmata.Aos.Engine.Validation;

internal static class AosJsonSchemaInstanceValidator
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly object RegistrationLock = new();

    public static SchemaValidationContext? TryCreateLocalContext(
        string repositoryRootPath,
        out string? errorMessage)
    {
        if (repositoryRootPath is null) throw new ArgumentNullException(nameof(repositoryRootPath));

        errorMessage = null;

        IReadOnlyList<AosLocalSchemaRegistryLoader.LocalSchema> localSchemas;
        try
        {
            localSchemas = AosLocalSchemaRegistryLoader.LoadLocalSchemas(repositoryRootPath);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            errorMessage = ex.Message;
            return null;
        }

        var byId = new Dictionary<string, JsonSchema>(StringComparer.Ordinal);

        foreach (var schema in localSchemas)
        {
            string schemaId;
            try
            {
                using var doc = JsonDocument.Parse(schema.Json);
                schemaId = ReadSchemaIdOrThrow(doc.RootElement, schema.FullPath);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                errorMessage = $"Local schema pack contains an invalid schema file '{schema.FileName}': {ex.Message}";
                return null;
            }

            // Allow ref resolution by $id.
            // NOTE: JsonSchema.Net resolves references via registries; the global registry is used as a fallback.
            JsonSchema parsed;
            try
            {
                var uri = new Uri(schemaId, UriKind.Absolute);
                lock (RegistrationLock)
                {
                    // Check if it's already registered to avoid "Overwriting registered schemas" error from JsonSchema.FromText
                    var existing = SchemaRegistry.Global.Get(uri);
                    if (existing is JsonSchema existingSchema)
                    {
                        parsed = existingSchema;
                    }
                    else
                    {
                        // Parse inside lock to prevent race condition where another thread registers it between Get and FromText
                        try 
                        {
                            parsed = JsonSchema.FromText(schema.Json);
                            // FromText might have registered it. If not, register it.
                            if (SchemaRegistry.Global.Get(uri) is null)
                            {
                                SchemaRegistry.Global.Register(uri, parsed);
                            }
                        }
                        catch (Exception ex)
                        {
                             errorMessage = $"Failed to parse/register local schema '{schema.FileName}' (id='{schemaId}'): {ex.Message}";
                             return null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to register local schema '{schema.FileName}' (id='{schemaId}') for reference resolution: {ex.Message}";
                return null;
            }

            if (!byId.TryAdd(schemaId, parsed))
            {
                errorMessage = $"Local schema pack contains duplicate '$id' value '{schemaId}'.";
                return null;
            }
        }

        return new SchemaValidationContext(byId);
    }

    public static IReadOnlyList<SchemaValidationIssue> ValidateJsonFileAgainstSchema(
        SchemaValidationContext ctx,
        string jsonFilePath,
        string schemaId)
    {
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));
        if (jsonFilePath is null) throw new ArgumentNullException(nameof(jsonFilePath));
        if (string.IsNullOrWhiteSpace(schemaId)) throw new ArgumentNullException(nameof(schemaId));

        if (!ctx.ById.TryGetValue(schemaId, out var schema))
        {
            return [new SchemaValidationIssue(InstanceLocation: "", Message: $"No schema found for schema id '{schemaId}' in the local schema pack.")];
        }

        string json;
        try
        {
            json = File.ReadAllText(jsonFilePath, Utf8NoBom);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [new SchemaValidationIssue(InstanceLocation: "", Message: $"Failed to read JSON file: {ex.Message}")];
        }

        JsonDocument? doc = null;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return [new SchemaValidationIssue(InstanceLocation: "", Message: $"Invalid JSON: {ex.Message}")];
        }

        if (doc is null)
        {
            return [new SchemaValidationIssue(InstanceLocation: "", Message: "Invalid JSON: parsed to null root node.")];
        }

        using (doc)
        {
            var options = new EvaluationOptions
            {
                OutputFormat = OutputFormat.List
            };

            var result = schema.Evaluate(doc.RootElement, options);
            if (result.IsValid)
            {
                return Array.Empty<SchemaValidationIssue>();
            }

            var issues = new List<SchemaValidationIssue>();
            CollectIssuesRecursive(result, issues);
            if (issues.Count == 0)
            {
                issues.Add(new SchemaValidationIssue(InstanceLocation: "", Message: "Schema validation failed."));
            }

            return issues;
        }
    }

    public static IReadOnlyList<SchemaValidationIssue> ValidateJsonElementAgainstSchema(
        SchemaValidationContext ctx,
        JsonElement element,
        string schemaId)
    {
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));
        if (string.IsNullOrWhiteSpace(schemaId)) throw new ArgumentNullException(nameof(schemaId));

        if (!ctx.ById.TryGetValue(schemaId, out var schema))
        {
            return [new SchemaValidationIssue(InstanceLocation: "", Message: $"No schema found for schema id '{schemaId}' in the local schema pack.")];
        }

        var options = new EvaluationOptions
        {
            OutputFormat = OutputFormat.List
        };

        var result = schema.Evaluate(element, options);
        if (result.IsValid)
        {
            return Array.Empty<SchemaValidationIssue>();
        }

        var issues = new List<SchemaValidationIssue>();
        CollectIssuesRecursive(result, issues);
        if (issues.Count == 0)
        {
            issues.Add(new SchemaValidationIssue(InstanceLocation: "", Message: "Schema validation failed."));
        }

        return issues;
    }

    private static string ReadSchemaIdOrThrow(JsonElement root, string sourcePath)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Schema root JSON value must be an object at '{sourcePath}'.");
        }

        if (!root.TryGetProperty("$id", out var idProp) || idProp.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Schema is missing required '$id' string at '{sourcePath}'.");
        }

        var id = idProp.GetString() ?? "";
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException($"Schema has an empty '$id' value at '{sourcePath}'.");
        }

        return id;
    }

    private static void CollectIssuesRecursive(EvaluationResults result, List<SchemaValidationIssue> output)
    {
        if (result.Errors is not null)
        {
            var pointer = result.InstanceLocation.ToString() ?? "";
            foreach (var kvp in result.Errors)
            {
                var msg = kvp.Value ?? "Schema validation failed.";
                output.Add(new SchemaValidationIssue(InstanceLocation: pointer, Message: msg));
            }
        }

        if (result.Details is null)
        {
            return;
        }

        foreach (var child in result.Details)
        {
            if (child is null) continue;
            CollectIssuesRecursive(child, output);
        }
    }

    internal sealed record SchemaValidationContext(IReadOnlyDictionary<string, JsonSchema> ById);

    internal sealed record SchemaValidationIssue(string InstanceLocation, string Message);
}

