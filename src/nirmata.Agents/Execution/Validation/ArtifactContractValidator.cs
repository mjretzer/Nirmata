using System.Reflection;
using System.Text.Json;
using nirmata.Agents.Models.Runtime;
using nirmata.Aos.Public;
using Json.Schema;

namespace nirmata.Agents.Execution.Validation;

internal static class ArtifactContractValidator
{
    private const string PhasePlanSchemaId = "nirmata:aos:schema:phase-plan:v1";
    private const string TaskPlanSchemaId = "nirmata:aos:schema:task-plan:v1";
    private const string VerifierInputSchemaId = "nirmata:aos:schema:verifier-input:v1";
    private const string VerifierOutputSchemaId = "nirmata:aos:schema:verifier-output:v1";
    private const string FixPlanSchemaId = "nirmata:aos:schema:fix-plan:v1";
    private const string DiagnosticSchemaId = "nirmata:aos:schema:diagnostic:v1";

    private const int CurrentSchemaVersion = 1;

    private static readonly Lazy<JsonSchema> PhasePlanSchema = new(() => LoadSchema("phase-plan.schema.json"));
    private static readonly Lazy<JsonSchema> TaskPlanSchema = new(() => LoadSchema("task-plan.schema.json"));
    private static readonly Lazy<JsonSchema> VerifierInputSchema = new(() => LoadSchema("verifier-input.schema.json"));
    private static readonly Lazy<JsonSchema> VerifierOutputSchema = new(() => LoadSchema("verifier-output.schema.json"));
    private static readonly Lazy<JsonSchema> FixPlanSchema = new(() => LoadSchema("fix-plan.schema.json"));
    private static readonly Lazy<JsonSchema> DiagnosticSchema = new(() => LoadSchema("diagnostic.schema.json"));

    public static ArtifactContractValidationResult ValidatePhasePlan(string artifactPath, string artifactJson, string aosRootPath, string readBoundary)
        => ValidateInternal(artifactPath, artifactJson, aosRootPath, readBoundary, PhasePlanSchema.Value, PhasePlanSchemaId, "phase-planning");

    public static ArtifactContractValidationResult ValidateTaskPlan(string artifactPath, string artifactJson, string aosRootPath, string readBoundary)
        => ValidateInternal(artifactPath, artifactJson, aosRootPath, readBoundary, TaskPlanSchema.Value, TaskPlanSchemaId, "task-execution");

    public static ArtifactContractValidationResult ValidateVerifierInput(string artifactPath, string artifactJson, string aosRootPath, string readBoundary)
        => ValidateInternal(artifactPath, artifactJson, aosRootPath, readBoundary, VerifierInputSchema.Value, VerifierInputSchemaId, "uat-verification");

    public static ArtifactContractValidationResult ValidateVerifierOutput(string artifactPath, string artifactJson, string aosRootPath, string readBoundary)
        => ValidateInternal(artifactPath, artifactJson, aosRootPath, readBoundary, VerifierOutputSchema.Value, VerifierOutputSchemaId, "uat-verification");

    public static ArtifactContractValidationResult ValidateFixPlan(string artifactPath, string artifactJson, string aosRootPath, string readBoundary)
        => ValidateInternal(artifactPath, artifactJson, aosRootPath, readBoundary, FixPlanSchema.Value, FixPlanSchemaId, "fix-planning");

    public static ArtifactContractValidationResult ValidateDiagnostic(string artifactPath, string artifactJson, string aosRootPath, string readBoundary)
        => ValidateInternal(artifactPath, artifactJson, aosRootPath, readBoundary, DiagnosticSchema.Value, DiagnosticSchemaId, "diagnostics");

    private static ArtifactContractValidationResult ValidateInternal(
        string artifactPath,
        string artifactJson,
        string aosRootPath,
        string readBoundary,
        JsonSchema schema,
        string schemaId,
        string phaseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(aosRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(readBoundary);

        if (string.IsNullOrWhiteSpace(artifactJson))
        {
            return WriteFailureDiagnostic(aosRootPath, artifactPath, readBoundary, schemaId, phaseName, null,
                [new ValidationError { Path = "$", Message = "Artifact JSON is empty." }]);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(artifactJson);
        }
        catch (JsonException ex)
        {
            return WriteFailureDiagnostic(aosRootPath, artifactPath, readBoundary, schemaId, phaseName, null,
                [new ValidationError { Path = "$", Message = $"Invalid JSON: {ex.Message}" }]);
        }

        using (document)
        {
            var declaredVersion = TryReadDeclaredSchemaVersion(document.RootElement);
            var evaluation = schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });

            if (evaluation.IsValid)
            {
                return ArtifactContractValidationResult.Valid();
            }

            var errors = CollectValidationIssues(evaluation);
            return WriteFailureDiagnostic(aosRootPath, artifactPath, readBoundary, schemaId, phaseName, declaredVersion, errors);
        }
    }

    private static ArtifactContractValidationResult WriteFailureDiagnostic(
        string aosRootPath,
        string artifactPath,
        string readBoundary,
        string schemaId,
        string phaseName,
        int? declaredVersion,
        IReadOnlyList<ValidationError> errors)
    {
        var diagnostic = new DiagnosticArtifact
        {
            SchemaVersion = 1,
            ArtifactPath = artifactPath,
            FailedSchemaId = schemaId,
            FailedSchemaVersion = declaredVersion ?? CurrentSchemaVersion,
            Timestamp = DateTimeOffset.UtcNow,
            Phase = phaseName,
            Context = new Dictionary<string, string>
            {
                { "readBoundary", readBoundary },
                { "originalArtifactPath", artifactPath }
            },
            ValidationErrors = errors.ToList(),
            RepairSuggestions = GenerateRepairSuggestions(schemaId, errors)
        };

        var diagnosticPath = DiagnosticArtifactWriter.Write(aosRootPath, diagnostic);

        return ArtifactContractValidationResult.Invalid(
            diagnosticPath,
            $"Artifact contract validation failed for {schemaId}. Ensure the artifact matches the canonical schema version {CurrentSchemaVersion}.",
            errors);
    }

    private static List<string> GenerateRepairSuggestions(string schemaId, IReadOnlyList<ValidationError> errors)
    {
        var suggestions = new List<string>();

        foreach (var error in errors)
        {
            if (error.Message.Contains("required", StringComparison.OrdinalIgnoreCase))
            {
                suggestions.Add($"Ensure the required property at '{error.Path}' is present in the artifact.");
            }
            else if (error.Message.Contains("type", StringComparison.OrdinalIgnoreCase))
            {
                suggestions.Add($"Check the data type at '{error.Path}'. Expected {error.Expected}, but found {error.Actual}.");
            }
        }

        if (schemaId == TaskPlanSchemaId)
        {
            suggestions.Add("For task plans, ensure 'fileScopes' is an array of objects with 'path', 'scopeType', and 'description' fields.");
            suggestions.Add("Verify that 'verificationSteps' include 'verificationType', 'description', and 'expectedOutcome'.");
        }
        else if (schemaId == PhasePlanSchemaId)
        {
            suggestions.Add("For phase plans, ensure the 'tasks' array contains valid task references with 'taskId' and 'title'.");
        }

        if (suggestions.Count == 0)
        {
            suggestions.Add("Review the validation errors and ensure the JSON matches the canonical schema.");
            suggestions.Add($"Check the schema documentation for {schemaId}.");
        }

        return suggestions.Distinct().ToList();
    }

    private static int? TryReadDeclaredSchemaVersion(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (!root.TryGetProperty("schemaVersion", out var versionProp)) return null;
        if (versionProp.ValueKind == JsonValueKind.Number && versionProp.TryGetInt32(out var version)) return version;
        return null;
    }

    private static IReadOnlyList<ValidationError> CollectValidationIssues(EvaluationResults results)
    {
        var issues = new List<ValidationError>();
        CollectValidationIssuesRecursive(results, issues);
        if (issues.Count == 0)
        {
            issues.Add(new ValidationError { Path = "$", Message = "Schema validation failed." });
        }
        return issues;
    }

    private static void CollectValidationIssuesRecursive(EvaluationResults results, ICollection<ValidationError> issues)
    {
        if (results.Errors is not null)
        {
            var path = string.IsNullOrWhiteSpace(results.InstanceLocation.ToString()) ? "$" : results.InstanceLocation.ToString();
            foreach (var error in results.Errors)
            {
                issues.Add(new ValidationError { Path = path, Message = error.Value });
            }
        }

        if (results.Details is null) return;
        foreach (var detail in results.Details)
        {
            CollectValidationIssuesRecursive(detail, issues);
        }
    }

    private static JsonSchema LoadSchema(string fileName)
    {
        var json = LoadEmbeddedSchema(fileName);
        return JsonSchema.FromText(json);
    }

    private static string LoadEmbeddedSchema(string fileName)
    {
        var assembly = typeof(ArtifactContractValidator).Assembly;
        
        // Try exact match first, then fallback to suffix match
        var resourceNames = assembly.GetManifestResourceNames();
        var resourceName = resourceNames.FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            throw new InvalidOperationException($"Schema resource matching '{fileName}' not found. Available resources: {string.Join(", ", resourceNames)}");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Could not open stream for resource '{resourceName}'.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

internal sealed class ArtifactContractValidationResult
{
    public bool IsValid { get; init; }

    public string? DiagnosticPath { get; init; }

    public string? Message { get; init; }

    public IReadOnlyList<ValidationError> Errors { get; init; } = Array.Empty<ValidationError>();

    public static ArtifactContractValidationResult Valid() => new()
    {
        IsValid = true
    };

    public static ArtifactContractValidationResult Invalid(string diagnosticPath, string message, IReadOnlyList<ValidationError> errors) => new()
    {
        IsValid = false,
        DiagnosticPath = diagnosticPath,
        Message = message,
        Errors = errors
    };

    public string CreateFailureMessage() =>
        $"{Message} Diagnostic: {DiagnosticPath}";
}

