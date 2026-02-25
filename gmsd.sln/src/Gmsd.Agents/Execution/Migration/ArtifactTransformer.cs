using System.Text.Json;
using System.Text.Json.Nodes;

namespace Gmsd.Agents.Execution.Migration;

/// <summary>
/// Transforms artifacts from old format to new canonical schema format.
/// </summary>
public static class ArtifactTransformer
{
    /// <summary>
    /// Transforms an artifact from old format to new canonical format.
    /// </summary>
    public static string TransformToNewFormat(string artifactJson, ArtifactType artifactType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactJson);

        return artifactType switch
        {
            ArtifactType.TaskPlan => TransformTaskPlan(artifactJson),
            ArtifactType.VerifierInput => TransformVerifierInput(artifactJson),
            ArtifactType.VerifierOutput => TransformVerifierOutput(artifactJson),
            ArtifactType.FixPlan => TransformFixPlan(artifactJson),
            ArtifactType.PhasePlan => TransformPhasePlan(artifactJson),
            _ => artifactJson
        };
    }

    /// <summary>
    /// Transforms a task plan from old format to new canonical format.
    /// </summary>
    private static string TransformTaskPlan(string oldJson)
    {
        using var doc = JsonDocument.Parse(oldJson);
        var root = doc.RootElement;
        var obj = new JsonObject();

        // Add schema information
        obj["schemaVersion"] = 1;
        obj["schemaId"] = "gmsd:aos:schema:task-plan:v1";

        // Copy or transform task plan fields
        if (root.TryGetProperty("taskId", out var taskId))
            obj["taskId"] = taskId.GetString();

        if (root.TryGetProperty("runId", out var runId))
            obj["runId"] = runId.GetString();

        // Transform fileScopes: ensure it's an array of objects with 'path' field
        if (root.TryGetProperty("fileScopes", out var fileScopes))
        {
            var scopesArray = new JsonArray();
            if (fileScopes.ValueKind == JsonValueKind.Array)
            {
                foreach (var scope in fileScopes.EnumerateArray())
                {
                    if (scope.ValueKind == JsonValueKind.String)
                    {
                        // Old format: string paths
                        scopesArray.Add(new JsonObject { ["path"] = scope.GetString() });
                    }
                    else if (scope.ValueKind == JsonValueKind.Object)
                    {
                        // Already object format, copy as-is
                        scopesArray.Add(JsonNode.Parse(scope.GetRawText()));
                    }
                }
            }
            obj["fileScopes"] = scopesArray;
        }

        // Copy verification steps
        if (root.TryGetProperty("verificationSteps", out var verificationSteps))
            obj["verificationSteps"] = JsonNode.Parse(verificationSteps.GetRawText());

        // Copy acceptance criteria
        if (root.TryGetProperty("acceptanceCriteria", out var acceptanceCriteria))
            obj["acceptanceCriteria"] = JsonNode.Parse(acceptanceCriteria.GetRawText());

        // Add timestamp if not present
        if (!root.TryGetProperty("timestamp", out _))
            obj["timestamp"] = DateTimeOffset.UtcNow.ToString("O");

        var options = new JsonSerializerOptions { WriteIndented = true };
        return obj.ToJsonString(options);
    }

    /// <summary>
    /// Transforms verifier input from old format to new canonical format.
    /// </summary>
    private static string TransformVerifierInput(string oldJson)
    {
        using var doc = JsonDocument.Parse(oldJson);
        var root = doc.RootElement;
        var obj = new JsonObject();

        // Add schema information
        obj["schemaVersion"] = 1;
        obj["schemaId"] = "gmsd:aos:schema:verifier-input:v1";

        // Copy task and run IDs
        if (root.TryGetProperty("taskId", out var taskId))
            obj["taskId"] = taskId.GetString();

        if (root.TryGetProperty("runId", out var runId))
            obj["runId"] = runId.GetString();

        // Copy acceptance criteria
        if (root.TryGetProperty("criteria", out var criteria))
            obj["acceptanceCriteria"] = JsonNode.Parse(criteria.GetRawText());
        else if (root.TryGetProperty("acceptanceCriteria", out var acceptanceCriteria))
            obj["acceptanceCriteria"] = JsonNode.Parse(acceptanceCriteria.GetRawText());

        // Copy file scopes
        if (root.TryGetProperty("fileScopes", out var fileScopes))
            obj["fileScopes"] = JsonNode.Parse(fileScopes.GetRawText());

        // Add timestamp if not present
        if (!root.TryGetProperty("timestamp", out _))
            obj["timestamp"] = DateTimeOffset.UtcNow.ToString("O");

        var options = new JsonSerializerOptions { WriteIndented = true };
        return obj.ToJsonString(options);
    }

    /// <summary>
    /// Transforms verifier output from old format to new canonical format.
    /// </summary>
    private static string TransformVerifierOutput(string oldJson)
    {
        using var doc = JsonDocument.Parse(oldJson);
        var root = doc.RootElement;
        var obj = new JsonObject();

        // Add schema information
        obj["schemaVersion"] = 1;
        obj["schemaId"] = "gmsd:aos:schema:verifier-output:v1";

        // Copy task and run IDs
        if (root.TryGetProperty("taskId", out var taskId))
            obj["taskId"] = taskId.GetString();

        if (root.TryGetProperty("runId", out var runId))
            obj["runId"] = runId.GetString();

        // Copy status
        if (root.TryGetProperty("status", out var status))
            obj["status"] = status.GetString();

        // Copy checks
        if (root.TryGetProperty("checks", out var checks))
            obj["checks"] = JsonNode.Parse(checks.GetRawText());

        // Add timestamp if not present
        if (!root.TryGetProperty("timestamp", out _))
            obj["timestamp"] = DateTimeOffset.UtcNow.ToString("O");

        var options = new JsonSerializerOptions { WriteIndented = true };
        return obj.ToJsonString(options);
    }

    /// <summary>
    /// Transforms fix plan from old format to new canonical format.
    /// </summary>
    private static string TransformFixPlan(string oldJson)
    {
        using var doc = JsonDocument.Parse(oldJson);
        var root = doc.RootElement;
        var obj = new JsonObject();

        // Add schema information
        obj["schemaVersion"] = 1;
        obj["schemaId"] = "gmsd:aos:schema:fix-plan:v1";

        // Copy task and run IDs
        if (root.TryGetProperty("taskId", out var taskId))
            obj["taskId"] = taskId.GetString();

        if (root.TryGetProperty("runId", out var runId))
            obj["runId"] = runId.GetString();

        // Copy fixes
        if (root.TryGetProperty("fixes", out var fixes))
            obj["fixes"] = JsonNode.Parse(fixes.GetRawText());

        // Copy verification steps
        if (root.TryGetProperty("verificationSteps", out var verificationSteps))
            obj["verificationSteps"] = JsonNode.Parse(verificationSteps.GetRawText());

        // Add timestamp if not present
        if (!root.TryGetProperty("timestamp", out _))
            obj["timestamp"] = DateTimeOffset.UtcNow.ToString("O");

        var options = new JsonSerializerOptions { WriteIndented = true };
        return obj.ToJsonString(options);
    }

    /// <summary>
    /// Transforms phase plan from old format to new canonical format.
    /// </summary>
    private static string TransformPhasePlan(string oldJson)
    {
        using var doc = JsonDocument.Parse(oldJson);
        var root = doc.RootElement;
        var obj = new JsonObject();

        // Add schema information
        obj["schemaVersion"] = 1;
        obj["schemaId"] = "gmsd:aos:schema:phase-plan:v1";

        // Copy tasks
        if (root.TryGetProperty("tasks", out var tasks))
            obj["tasks"] = JsonNode.Parse(tasks.GetRawText());

        // Copy file scopes
        if (root.TryGetProperty("fileScopes", out var fileScopes))
        {
            var scopesArray = new JsonArray();
            if (fileScopes.ValueKind == JsonValueKind.Array)
            {
                foreach (var scope in fileScopes.EnumerateArray())
                {
                    if (scope.ValueKind == JsonValueKind.String)
                    {
                        scopesArray.Add(new JsonObject { ["path"] = scope.GetString() });
                    }
                    else if (scope.ValueKind == JsonValueKind.Object)
                    {
                        scopesArray.Add(JsonNode.Parse(scope.GetRawText()));
                    }
                }
            }
            obj["fileScopes"] = scopesArray;
        }

        // Copy verification steps
        if (root.TryGetProperty("verificationSteps", out var verificationSteps))
            obj["verificationSteps"] = JsonNode.Parse(verificationSteps.GetRawText());

        // Add timestamp if not present
        if (!root.TryGetProperty("timestamp", out _))
            obj["timestamp"] = DateTimeOffset.UtcNow.ToString("O");

        var options = new JsonSerializerOptions { WriteIndented = true };
        return obj.ToJsonString(options);
    }
}
