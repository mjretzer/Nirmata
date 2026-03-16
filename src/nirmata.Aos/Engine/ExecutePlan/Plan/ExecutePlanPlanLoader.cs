using System.Text.Json;

namespace nirmata.Aos.Engine.ExecutePlan;

internal static class ExecutePlanPlanLoader
{
    public static ExecutePlanPlan LoadFromFile(string planPath)
    {
        if (string.IsNullOrWhiteSpace(planPath))
        {
            throw new ExecutePlanPlanLoadException("Missing plan path.");
        }

        if (!File.Exists(planPath))
        {
            throw new ExecutePlanPlanLoadException($"Plan file not found at '{planPath}'.");
        }

        string json;
        try
        {
            json = File.ReadAllText(planPath);
        }
        catch (Exception ex)
        {
            throw new ExecutePlanPlanLoadException($"Failed to read plan file at '{planPath}'.", ex);
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            var location = ex.LineNumber is not null
                ? $" (line {ex.LineNumber}, byte {ex.BytePositionInLine})"
                : string.Empty;
            throw new ExecutePlanPlanLoadException($"Plan file is not valid JSON at '{planPath}': {ex.Message}{location}.", ex);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new ExecutePlanPlanLoadException("Plan JSON root MUST be an object.");
            }

            var schemaVersion = ReadRequiredInt32(root, "schemaVersion", context: "plan");
            if (schemaVersion != 1)
            {
                throw new ExecutePlanPlanLoadException($"Unsupported plan schemaVersion '{schemaVersion}'. Expected 1.");
            }

            if (!root.TryGetProperty("outputs", out var outputsEl))
            {
                throw new ExecutePlanPlanLoadException("Plan MUST include an 'outputs' array.");
            }

            if (outputsEl.ValueKind != JsonValueKind.Array)
            {
                throw new ExecutePlanPlanLoadException("Plan 'outputs' MUST be an array.");
            }

            var outputs = new List<ExecutePlanPlanOutput>();

            var index = 0;
            foreach (var outputEl in outputsEl.EnumerateArray())
            {
                if (outputEl.ValueKind != JsonValueKind.Object)
                {
                    throw new ExecutePlanPlanLoadException($"Plan outputs[{index}] MUST be an object.");
                }

                var relativePath = ReadRequiredString(outputEl, "relativePath", context: $"outputs[{index}]");
                if (string.IsNullOrWhiteSpace(relativePath))
                {
                    throw new ExecutePlanPlanLoadException($"Plan outputs[{index}].relativePath MUST be a non-empty string.");
                }

                if (!ExecutePlanOutputPathPolicy.TryValidateRelativePath(relativePath, out var pathError))
                {
                    throw new ExecutePlanPlanLoadException(
                        $"Plan outputs[{index}].relativePath is not allowed: {pathError} Value: '{relativePath}'."
                    );
                }

                var contentsUtf8 = ReadRequiredString(outputEl, "contentsUtf8", context: $"outputs[{index}]");
                outputs.Add(new ExecutePlanPlanOutput(relativePath, contentsUtf8));
                index++;
            }

            return new ExecutePlanPlan(schemaVersion, outputs);
        }
    }

    private static int ReadRequiredInt32(JsonElement obj, string propertyName, string context)
    {
        if (!obj.TryGetProperty(propertyName, out var el))
        {
            throw new ExecutePlanPlanLoadException($"{context} MUST include '{propertyName}'.");
        }

        if (el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var value))
        {
            throw new ExecutePlanPlanLoadException($"{context} '{propertyName}' MUST be an integer.");
        }

        return value;
    }

    private static string ReadRequiredString(JsonElement obj, string propertyName, string context)
    {
        if (!obj.TryGetProperty(propertyName, out var el))
        {
            throw new ExecutePlanPlanLoadException($"{context} MUST include '{propertyName}'.");
        }

        if (el.ValueKind != JsonValueKind.String)
        {
            throw new ExecutePlanPlanLoadException($"{context} '{propertyName}' MUST be a string.");
        }

        return el.GetString() ?? string.Empty;
    }
}

