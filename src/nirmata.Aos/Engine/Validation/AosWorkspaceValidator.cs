using System.Text.Json;
using nirmata.Aos.Engine.Config;
using nirmata.Aos.Engine.Paths;
using nirmata.Aos.Engine.Policy;
using nirmata.Aos.Public.Catalogs;

namespace nirmata.Aos.Engine.Validation;

internal enum AosWorkspaceLayer
{
    Spec,
    State,
    Evidence,
    Codebase,
    Context,
    Config
}

internal static class AosWorkspaceValidator
{
    public static AosWorkspaceValidationReport Validate(
        string repositoryRootPath,
        IEnumerable<AosWorkspaceLayer>? layers = null)
    {
        if (repositoryRootPath is null) throw new ArgumentNullException(nameof(repositoryRootPath));

        var selectedLayers = (layers ?? Enum.GetValues<AosWorkspaceLayer>()).Distinct().ToArray();

        var aosRootPath = Path.Combine(repositoryRootPath, ".aos");
        if (!Directory.Exists(aosRootPath))
        {
            return new AosWorkspaceValidationReport(
                RepositoryRootPath: repositoryRootPath,
                AosRootPath: aosRootPath,
                Layers: selectedLayers,
                Issues:
                [
                    new AosWorkspaceValidationIssue(
                        Layer: null,
                        ContractPath: ".aos/",
                        Message: "Missing AOS workspace root directory."
                    )
                ]
            );
        }

        var issues = new List<AosWorkspaceValidationIssue>();

        if (TryValidateInvariants(aosRootPath, out var invariantIssue))
        {
            issues.Add(invariantIssue);
            return new AosWorkspaceValidationReport(
                RepositoryRootPath: repositoryRootPath,
                AosRootPath: aosRootPath,
                Layers: selectedLayers,
                Issues: issues
            );
        }

        AosJsonSchemaInstanceValidator.SchemaValidationContext? schemaCtx = null;
        if (selectedLayers.Contains(AosWorkspaceLayer.Spec) ||
            selectedLayers.Contains(AosWorkspaceLayer.State) ||
            selectedLayers.Contains(AosWorkspaceLayer.Evidence) ||
            selectedLayers.Contains(AosWorkspaceLayer.Context) ||
            selectedLayers.Contains(AosWorkspaceLayer.Config))
        {
            schemaCtx = AosJsonSchemaInstanceValidator.TryCreateLocalContext(repositoryRootPath, out var schemaError);
            if (schemaCtx is null)
            {
                issues.Add(new AosWorkspaceValidationIssue(
                    Layer: null,
                    ContractPath: ".aos/schemas/registry.json",
                    Message: $"Unable to load local schema pack for schema-based validation: {schemaError}"
                ));
            }
        }

        foreach (var layer in selectedLayers)
        {
            switch (layer)
            {
                case AosWorkspaceLayer.Spec:
                    ValidateSpecLayer(aosRootPath, issues, schemaCtx);
                    break;
                case AosWorkspaceLayer.State:
                    ValidateStateLayer(aosRootPath, issues, schemaCtx);
                    break;
                case AosWorkspaceLayer.Evidence:
                    ValidateEvidenceLayer(aosRootPath, issues, schemaCtx);
                    break;
                case AosWorkspaceLayer.Codebase:
                    // No schema-validated artifacts for this layer in this milestone.
                    // (Invariants are handled separately.)
                    break;
                case AosWorkspaceLayer.Context:
                    ValidateContextLayer(aosRootPath, issues, schemaCtx);
                    break;
                case AosWorkspaceLayer.Config:
                    ValidateConfigLayer(aosRootPath, issues, schemaCtx);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(layer), layer, "Unknown layer.");
            }
        }

        return new AosWorkspaceValidationReport(
            RepositoryRootPath: repositoryRootPath,
            AosRootPath: aosRootPath,
            Layers: selectedLayers,
            Issues: issues
        );
    }

    private static bool TryValidateInvariants(string aosRootPath, out AosWorkspaceValidationIssue issue)
    {
        // Invariants are cross-file rules that gate the workspace model. We fail fast on any invariant breach.

        var projectSpecPath = Path.Combine(aosRootPath, "spec", "project.json");
        if (!File.Exists(projectSpecPath) && !Directory.Exists(projectSpecPath))
        {
            issue = new AosWorkspaceValidationIssue(
                Layer: AosWorkspaceLayer.Spec,
                ContractPath: ".aos/spec/project.json",
                Message: "Missing required artifact for single-project workspace."
            );
            return true;
        }

        var projectsSpecPath = Path.Combine(aosRootPath, "spec", "projects.json");
        if (File.Exists(projectsSpecPath) || Directory.Exists(projectsSpecPath))
        {
            issue = new AosWorkspaceValidationIssue(
                Layer: AosWorkspaceLayer.Spec,
                ContractPath: ".aos/spec/projects.json",
                Message: "Forbidden multi-project artifact exists; only .aos/spec/project.json is permitted."
            );
            return true;
        }

        var activeProjectStatePath = Path.Combine(aosRootPath, "state", "active-project.json");
        if (File.Exists(activeProjectStatePath) || Directory.Exists(activeProjectStatePath))
        {
            issue = new AosWorkspaceValidationIssue(
                Layer: AosWorkspaceLayer.State,
                ContractPath: ".aos/state/active-project.json",
                Message: "Forbidden multi-project artifact exists; only a single-project workspace is permitted."
            );
            return true;
        }

        var roadmapPath = Path.Combine(aosRootPath, "spec", "roadmap.json");
        if (File.Exists(roadmapPath))
        {
            try
            {
                using var stream = File.OpenRead(roadmapPath);
                using var doc = JsonDocument.Parse(stream);

                if (ContainsMultiProjectReference(doc.RootElement))
                {
                    issue = new AosWorkspaceValidationIssue(
                        Layer: AosWorkspaceLayer.Spec,
                        ContractPath: ".aos/spec/roadmap.json",
                        Message: "Roadmap references multiple projects; only a single project is permitted."
                    );
                    return true;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                issue = new AosWorkspaceValidationIssue(
                    Layer: AosWorkspaceLayer.Spec,
                    ContractPath: ".aos/spec/roadmap.json",
                    Message: "Invalid JSON."
                );
                return true;
            }
        }

        issue = new AosWorkspaceValidationIssue(Layer: null, ContractPath: "", Message: "");
        return false;
    }

    private static bool ContainsMultiProjectReference(JsonElement root)
    {
        // Best-effort invariant enforcement without a dedicated roadmap schema in this milestone.
        // We detect "multi-project" by finding common "projects"/"projectIds"/"projectFiles"/"projectRefs" arrays with > 1 item.
        return ContainsMultiProjectReferenceRecursive(root);
    }

    private static bool ContainsMultiProjectReferenceRecursive(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var normalized = NormalizePropertyName(prop.Name);
                    if (IsProjectReferenceCollectionProperty(normalized) && prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        var count = prop.Value.GetArrayLength();
                        if (count > 1)
                        {
                            return true;
                        }
                    }

                    if (ContainsMultiProjectReferenceRecursive(prop.Value))
                    {
                        return true;
                    }
                }

                return false;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (ContainsMultiProjectReferenceRecursive(item))
                    {
                        return true;
                    }
                }

                return false;

            default:
                return false;
        }
    }

    private static string NormalizePropertyName(string name) =>
        (name ?? "")
            .Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();

    private static bool IsProjectReferenceCollectionProperty(string normalizedPropertyName) =>
        normalizedPropertyName is "projects" or "projectids" or "projectfiles" or "projectrefs";

    private static void ValidateSpecLayer(
        string aosRootPath,
        List<AosWorkspaceValidationIssue> issues,
        AosJsonSchemaInstanceValidator.SchemaValidationContext? schemaCtx)
    {
        ValidateJsonFile(
            layer: AosWorkspaceLayer.Spec,
            contractPath: ".aos/spec/project.json",
            filePath: Path.Combine(aosRootPath, "spec", "project.json"),
            issues: issues,
            required: true,
            schemaCtx: schemaCtx,
            schemaId: SchemaIds.ProjectV1
        );

        ValidateJsonFile(
            layer: AosWorkspaceLayer.Spec,
            contractPath: ".aos/spec/milestones/index.json",
            filePath: Path.Combine(aosRootPath, "spec", "milestones", "index.json"),
            issues: issues,
            required: true,
            schemaCtx: schemaCtx,
            schemaId: SchemaIds.CatalogIndexV1
        );

        ValidateJsonFile(
            layer: AosWorkspaceLayer.Spec,
            contractPath: ".aos/spec/phases/index.json",
            filePath: Path.Combine(aosRootPath, "spec", "phases", "index.json"),
            issues: issues,
            required: true,
            schemaCtx: schemaCtx,
            schemaId: SchemaIds.CatalogIndexV1
        );

        ValidateJsonFile(
            layer: AosWorkspaceLayer.Spec,
            contractPath: ".aos/spec/tasks/index.json",
            filePath: Path.Combine(aosRootPath, "spec", "tasks", "index.json"),
            issues: issues,
            required: true,
            schemaCtx: schemaCtx,
            schemaId: SchemaIds.CatalogIndexV1
        );

        ValidateJsonFile(
            layer: AosWorkspaceLayer.Spec,
            contractPath: ".aos/spec/issues/index.json",
            filePath: Path.Combine(aosRootPath, "spec", "issues", "index.json"),
            issues: issues,
            required: true,
            schemaCtx: schemaCtx,
            schemaId: SchemaIds.CatalogIndexV1
        );

        ValidateJsonFile(
            layer: AosWorkspaceLayer.Spec,
            contractPath: ".aos/spec/uat/index.json",
            filePath: Path.Combine(aosRootPath, "spec", "uat", "index.json"),
            issues: issues,
            required: true,
            schemaCtx: schemaCtx,
            schemaId: SchemaIds.CatalogIndexV1
        );

        // Optional in this milestone; if present, it must be valid JSON.
        ValidateJsonFile(
            layer: AosWorkspaceLayer.Spec,
            contractPath: ".aos/spec/roadmap.json",
            filePath: Path.Combine(aosRootPath, "spec", "roadmap.json"),
            issues: issues,
            required: true,
            schemaCtx: schemaCtx,
            schemaId: SchemaIds.RoadmapV1
        );

        ValidateRoadmapItemReferences(aosRootPath, issues);

        // Validate Issue files in .aos/spec/issues/ (optional, but must be valid if present)
        var issuesDir = Path.Combine(aosRootPath, "spec", "issues");
        if (Directory.Exists(issuesDir))
        {
            foreach (var filePath in Directory.EnumerateFiles(issuesDir, "ISS-*.json", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(filePath);
                if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".json", StringComparison.Ordinal))
                {
                    continue;
                }

                var stem = fileName[..^".json".Length];
                // Validate ISS-*.json naming pattern
                if (!stem.StartsWith("ISS-", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var contractPath = $".aos/spec/issues/{fileName}";
                ValidateJsonFile(
                    layer: AosWorkspaceLayer.Spec,
                    contractPath: contractPath,
                    filePath: filePath,
                    issues: issues,
                    required: false,
                    schemaCtx: schemaCtx,
                    schemaId: SchemaIds.IssueTriageV1
                );
            }
        }
    }

    private static void ValidateStateLayer(
        string aosRootPath,
        List<AosWorkspaceValidationIssue> issues,
        AosJsonSchemaInstanceValidator.SchemaValidationContext? schemaCtx)
    {
        ValidateJsonFile(
            layer: AosWorkspaceLayer.State,
            contractPath: ".aos/state/state.json",
            filePath: Path.Combine(aosRootPath, "state", "state.json"),
            issues: issues,
            required: true,
            schemaCtx: schemaCtx,
            schemaId: SchemaIds.StateSnapshotV1
        );

        ValidateStateCursorReference(aosRootPath, issues);

        ValidateNdjsonFile(
            layer: AosWorkspaceLayer.State,
            contractPath: ".aos/state/events.ndjson",
            filePath: Path.Combine(aosRootPath, "state", "events.ndjson"),
            issues: issues,
            required: true,
            schemaCtx: schemaCtx,
            schemaId: SchemaIds.EventV1
        );
    }

    private static void ValidateEvidenceLayer(
        string aosRootPath,
        List<AosWorkspaceValidationIssue> issues,
        AosJsonSchemaInstanceValidator.SchemaValidationContext? schemaCtx)
    {
        ValidateJsonFile(
            layer: AosWorkspaceLayer.Evidence,
            contractPath: ".aos/evidence/logs/commands.json",
            filePath: Path.Combine(aosRootPath, "evidence", "logs", "commands.json"),
            issues: issues,
            required: true,
            schemaCtx: schemaCtx,
            schemaId: SchemaIds.CommandsLogV1
        );

        ValidateJsonFile(
            layer: AosWorkspaceLayer.Evidence,
            contractPath: ".aos/evidence/runs/index.json",
            filePath: Path.Combine(aosRootPath, "evidence", "runs", "index.json"),
            issues: issues,
            required: true,
            schemaCtx: schemaCtx,
            schemaId: SchemaIds.RunsIndexV1
        );

        ValidateRunManifests(aosRootPath, issues);
        ValidateRunSummaryAndCommandsViews(aosRootPath, issues, schemaCtx);
        ValidateTaskEvidenceLatestPointers(aosRootPath, issues, schemaCtx);
    }

    private static void ValidateRunSummaryAndCommandsViews(
        string aosRootPath,
        List<AosWorkspaceValidationIssue> issues,
        AosJsonSchemaInstanceValidator.SchemaValidationContext? schemaCtx)
    {
        var runsRootPath = Path.Combine(aosRootPath, "evidence", "runs");
        if (!Directory.Exists(runsRootPath))
        {
            return;
        }

        foreach (var runDir in Directory.EnumerateDirectories(runsRootPath, "*", SearchOption.TopDirectoryOnly))
        {
            var dirName = Path.GetFileName(runDir);
            if (string.IsNullOrWhiteSpace(dirName))
            {
                continue;
            }

            if (!AosPathRouter.TryParseArtifactId(dirName, out var kind, out var normalizedId, out _)
                || kind != AosArtifactKind.Run
                || !string.Equals(normalizedId, dirName, StringComparison.Ordinal))
            {
                // Ignore non-canonical run directories; repair/validators cover these elsewhere.
                continue;
            }

            ValidateJsonFile(
                layer: AosWorkspaceLayer.Evidence,
                contractPath: $".aos/evidence/runs/{dirName}/summary.json",
                filePath: AosPathRouter.GetRunSummaryPath(aosRootPath, dirName),
                issues: issues,
                required: false,
                schemaCtx: schemaCtx,
                schemaId: SchemaIds.RunSummaryV1
            );

            ValidateJsonFile(
                layer: AosWorkspaceLayer.Evidence,
                contractPath: $".aos/evidence/runs/{dirName}/commands.json",
                filePath: AosPathRouter.GetRunCommandsPath(aosRootPath, dirName),
                issues: issues,
                required: false,
                schemaCtx: schemaCtx,
                schemaId: SchemaIds.RunCommandsV1
            );
        }
    }

    private static void ValidateTaskEvidenceLatestPointers(
        string aosRootPath,
        List<AosWorkspaceValidationIssue> issues,
        AosJsonSchemaInstanceValidator.SchemaValidationContext? schemaCtx)
    {
        var root = Path.Combine(aosRootPath, "evidence", "task-evidence");
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var taskDir in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
        {
            var dirName = Path.GetFileName(taskDir);
            if (string.IsNullOrWhiteSpace(dirName))
            {
                continue;
            }

            if (!AosPathRouter.TryParseArtifactId(dirName, out var kind, out var normalizedId, out _)
                || kind != AosArtifactKind.Task
                || !string.Equals(normalizedId, dirName, StringComparison.Ordinal))
            {
                // Ignore non-canonical task folders.
                continue;
            }

            ValidateJsonFile(
                layer: AosWorkspaceLayer.Evidence,
                contractPath: $".aos/evidence/task-evidence/{dirName}/latest.json",
                filePath: Path.Combine(taskDir, "latest.json"),
                issues: issues,
                required: false,
                schemaCtx: schemaCtx,
                schemaId: SchemaIds.TaskEvidenceLatestV1
            );
        }
    }

    private static void ValidateConfigLayer(
        string aosRootPath,
        List<AosWorkspaceValidationIssue> issues,
        AosJsonSchemaInstanceValidator.SchemaValidationContext? schemaCtx)
    {
        // Config is optional for baseline workspace validity in this milestone.
        // If present, it MUST validate against the local schema pack.
        ValidateJsonFile(
            layer: AosWorkspaceLayer.Config,
            contractPath: AosConfigLoader.ConfigContractPath,
            filePath: Path.Combine(aosRootPath, "config", "config.json"),
            issues: issues,
            required: false,
            schemaCtx: schemaCtx,
            schemaId: SchemaIds.ConfigV1
        );

        // Policy is required by execute-plan and other mutating commands; fail fast if missing.
        // If present, it MUST validate against the local schema pack.
        ValidateJsonFile(
            layer: AosWorkspaceLayer.Config,
            contractPath: AosPolicyLoader.PolicyContractPath,
            filePath: Path.Combine(aosRootPath, "config", "policy.json"),
            issues: issues,
            required: false,
            schemaCtx: schemaCtx,
            schemaId: SchemaIds.PolicyV1
        );
    }

    private static void ValidateContextLayer(
        string aosRootPath,
        List<AosWorkspaceValidationIssue> issues,
        AosJsonSchemaInstanceValidator.SchemaValidationContext? schemaCtx)
    {
        // Context packs are optional; if present, canonical pack files MUST validate against the local schema pack.
        // Canonical pack file: `.aos/context/packs/PCK-####.json`.
        var packsRoot = Path.Combine(aosRootPath, "context", "packs");
        if (Directory.Exists(packsRoot))
        {
            foreach (var filePath in Directory.EnumerateFiles(packsRoot, "PCK-*.json", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(filePath);
                if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".json", StringComparison.Ordinal))
                {
                    continue;
                }

                var stem = fileName[..^".json".Length];
                if (!AosPathRouter.TryParseArtifactId(stem, out var kind, out var normalizedId, out _)
                    || kind != AosArtifactKind.ContextPack
                    || !string.Equals(normalizedId, stem, StringComparison.Ordinal))
                {
                    // Ignore non-canonical pack files.
                    continue;
                }

                var contractPath = AosPathRouter.GetContractPath(AosArtifactKind.ContextPack, normalizedId);
                ValidateJsonFile(
                    layer: AosWorkspaceLayer.Context,
                    contractPath: contractPath,
                    filePath: filePath,
                    issues: issues,
                    required: false,
                    schemaCtx: schemaCtx,
                    schemaId: SchemaIds.ContextPackV1
                );
            }
        }

        // Validate TODO files in .aos/context/todos/ (optional, but must be valid if present)
        var todosRoot = Path.Combine(aosRootPath, "context", "todos");
        if (Directory.Exists(todosRoot))
        {
            foreach (var filePath in Directory.EnumerateFiles(todosRoot, "TODO-*.json", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(filePath);
                if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".json", StringComparison.Ordinal))
                {
                    continue;
                }

                var stem = fileName[..^".json".Length];
                // Validate TODO-*.json naming pattern
                if (!stem.StartsWith("TODO-", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var contractPath = $".aos/context/todos/{fileName}";
                ValidateJsonFile(
                    layer: AosWorkspaceLayer.Context,
                    contractPath: contractPath,
                    filePath: filePath,
                    issues: issues,
                    required: false,
                    schemaCtx: schemaCtx,
                    schemaId: SchemaIds.TodoV1
                );
            }
        }
    }

    private static void ValidateRunManifests(string aosRootPath, List<AosWorkspaceValidationIssue> issues)
    {
        var runsRootPath = Path.Combine(aosRootPath, "evidence", "runs");
        if (!Directory.Exists(runsRootPath))
        {
            return;
        }

        foreach (var runDir in Directory.EnumerateDirectories(runsRootPath, "*", SearchOption.TopDirectoryOnly))
        {
            var dirName = Path.GetFileName(runDir);
            if (string.IsNullOrWhiteSpace(dirName))
            {
                continue;
            }

            var canonicalRunJsonPath = AosPathRouter.GetRunMetadataPath(aosRootPath, dirName);
            var legacyRunJsonPath = AosPathRouter.GetLegacyRunMetadataPath(aosRootPath, dirName);
            var runJsonPath = File.Exists(canonicalRunJsonPath) ? canonicalRunJsonPath : legacyRunJsonPath;

            var canonicalManifestPath = AosPathRouter.GetRunManifestPath(aosRootPath, dirName);
            var legacyManifestPath = AosPathRouter.GetLegacyRunManifestPath(aosRootPath, dirName);

            // If run.json doesn't exist, we can't infer status; still validate manifest if present.
            if (!File.Exists(runJsonPath))
            {
                if (File.Exists(canonicalManifestPath))
                {
                    ValidateJsonFile(
                        layer: AosWorkspaceLayer.Evidence,
                        contractPath: $".aos/evidence/runs/{dirName}/artifacts/manifest.json",
                        filePath: canonicalManifestPath,
                        issues: issues,
                        required: false
                    );
                }
                else if (File.Exists(legacyManifestPath))
                {
                    ValidateJsonFile(
                        layer: AosWorkspaceLayer.Evidence,
                        contractPath: $".aos/evidence/runs/{dirName}/manifest.json",
                        filePath: legacyManifestPath,
                        issues: issues,
                        required: false
                    );
                }
                continue;
            }

            if (Directory.Exists(runJsonPath))
            {
                issues.Add(new AosWorkspaceValidationIssue(
                    Layer: AosWorkspaceLayer.Evidence,
                    ContractPath: File.Exists(canonicalRunJsonPath)
                        ? $".aos/evidence/runs/{dirName}/artifacts/run.json"
                        : $".aos/evidence/runs/{dirName}/run.json",
                    Message: "Expected file, found directory."
                ));
                continue;
            }

            JsonDocument runDoc;
            try
            {
                using var stream = File.OpenRead(runJsonPath);
                runDoc = JsonDocument.Parse(stream);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                issues.Add(new AosWorkspaceValidationIssue(
                    Layer: AosWorkspaceLayer.Evidence,
                    ContractPath: File.Exists(canonicalRunJsonPath)
                        ? $".aos/evidence/runs/{dirName}/artifacts/run.json"
                        : $".aos/evidence/runs/{dirName}/run.json",
                    Message: "Invalid JSON."
                ));
                continue;
            }

            using (runDoc)
            {
                var status = TryGetStringProperty(runDoc.RootElement, "status");
                var isFinished = string.Equals(status, "finished", StringComparison.OrdinalIgnoreCase);

                // For finished runs, the manifest is required. For started runs, the manifest is optional.
                // During transition, tolerate either canonical or legacy manifest location.
                if (File.Exists(canonicalManifestPath))
                {
                    ValidateJsonFile(
                        layer: AosWorkspaceLayer.Evidence,
                        contractPath: $".aos/evidence/runs/{dirName}/artifacts/manifest.json",
                        filePath: canonicalManifestPath,
                        issues: issues,
                        required: isFinished
                    );
                }
                else if (File.Exists(legacyManifestPath))
                {
                    ValidateJsonFile(
                        layer: AosWorkspaceLayer.Evidence,
                        contractPath: $".aos/evidence/runs/{dirName}/manifest.json",
                        filePath: legacyManifestPath,
                        issues: issues,
                        required: isFinished
                    );
                }
                else if (isFinished)
                {
                    issues.Add(new AosWorkspaceValidationIssue(
                        Layer: AosWorkspaceLayer.Evidence,
                        ContractPath: $".aos/evidence/runs/{dirName}/artifacts/manifest.json",
                        Message: "Missing required file."
                    ));
                }
            }
        }
    }

    private static string? TryGetStringProperty(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static void ValidateJsonFile(
        AosWorkspaceLayer layer,
        string contractPath,
        string filePath,
        List<AosWorkspaceValidationIssue> issues,
        bool required,
        AosJsonSchemaInstanceValidator.SchemaValidationContext? schemaCtx = null,
        string? schemaId = null)
    {
        if (File.Exists(filePath))
        {
            if (Directory.Exists(filePath))
            {
                issues.Add(new AosWorkspaceValidationIssue(layer, contractPath, "Expected file, found directory."));
                return;
            }

            try
            {
                using var stream = File.OpenRead(filePath);
                using var _ = JsonDocument.Parse(stream);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                issues.Add(new AosWorkspaceValidationIssue(layer, contractPath, "Invalid JSON."));
                return;
            }

            if (schemaCtx is not null && !string.IsNullOrWhiteSpace(schemaId))
            {
                var schemaIssues = AosJsonSchemaInstanceValidator.ValidateJsonFileAgainstSchema(
                    schemaCtx,
                    jsonFilePath: filePath,
                    schemaId: schemaId
                );

                foreach (var schemaIssue in schemaIssues)
                {
                    var loc = string.IsNullOrWhiteSpace(schemaIssue.InstanceLocation) ? "/" : schemaIssue.InstanceLocation;
                    issues.Add(new AosWorkspaceValidationIssue(
                        layer,
                        contractPath,
                        schemaIssue.Message,
                        SchemaId: schemaId,
                        InstanceLocation: loc
                    ));
                }
            }

            return;
        }

        if (Directory.Exists(filePath))
        {
            issues.Add(new AosWorkspaceValidationIssue(layer, contractPath, "Expected file, found directory."));
            return;
        }

        if (required)
        {
            issues.Add(new AosWorkspaceValidationIssue(layer, contractPath, "Missing required file."));
        }
    }

    private static void ValidateNdjsonFile(
        AosWorkspaceLayer layer,
        string contractPath,
        string filePath,
        List<AosWorkspaceValidationIssue> issues,
        bool required,
        AosJsonSchemaInstanceValidator.SchemaValidationContext? schemaCtx = null,
        string? schemaId = null)
    {
        if (File.Exists(filePath))
        {
            if (Directory.Exists(filePath))
            {
                issues.Add(new AosWorkspaceValidationIssue(layer, contractPath, "Expected file, found directory."));
                return;
            }

            try
            {
                // Allow empty file. For non-empty lines:
                // - each line must be valid JSON
                // - each line must be a JSON object
                // - each line must validate against the local schema pack when provided
                var lineNo = 0;
                foreach (var line in File.ReadLines(filePath))
                {
                    lineNo++;
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    using var doc = JsonDocument.Parse(line);
                    if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        issues.Add(new AosWorkspaceValidationIssue(
                            layer,
                            contractPath,
                            $"Invalid NDJSON: non-empty line {lineNo} is not a JSON object."
                        ));
                        continue;
                    }

                    if (schemaCtx is not null && !string.IsNullOrWhiteSpace(schemaId))
                    {
                        var schemaIssues = AosJsonSchemaInstanceValidator.ValidateJsonElementAgainstSchema(
                            schemaCtx,
                            doc.RootElement,
                            schemaId
                        );

                        foreach (var schemaIssue in schemaIssues)
                        {
                            var pointer = schemaIssue.InstanceLocation ?? "";
                            var loc = string.IsNullOrWhiteSpace(pointer)
                                ? $"/lines/{lineNo}"
                                : $"/lines/{lineNo}{pointer}";

                            issues.Add(new AosWorkspaceValidationIssue(
                                layer,
                                contractPath,
                                schemaIssue.Message,
                                SchemaId: schemaId,
                                InstanceLocation: loc
                            ));
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                issues.Add(new AosWorkspaceValidationIssue(layer, contractPath, "Failed to read file."));
            }
            catch (JsonException)
            {
                // We don't currently get the line number from JsonException reliably; re-scan to find the first bad line.
                var lineNo = 0;
                foreach (var line in File.ReadLines(filePath))
                {
                    lineNo++;
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    try
                    {
                        using var _ = JsonDocument.Parse(line);
                    }
                    catch (JsonException)
                    {
                        issues.Add(new AosWorkspaceValidationIssue(
                            layer,
                            contractPath,
                            $"Invalid NDJSON: non-empty line {lineNo} is not valid JSON."
                        ));
                        return;
                    }
                }

                issues.Add(new AosWorkspaceValidationIssue(layer, contractPath, "Invalid NDJSON."));
            }

            return;
        }

        if (Directory.Exists(filePath))
        {
            issues.Add(new AosWorkspaceValidationIssue(layer, contractPath, "Expected file, found directory."));
            return;
        }

        if (required)
        {
            issues.Add(new AosWorkspaceValidationIssue(layer, contractPath, "Missing required file."));
        }
    }

    private static void ValidateRoadmapItemReferences(string aosRootPath, List<AosWorkspaceValidationIssue> issues)
    {
        var roadmapJsonPath = Path.Combine(aosRootPath, "spec", "roadmap.json");
        if (!File.Exists(roadmapJsonPath) || Directory.Exists(roadmapJsonPath))
        {
            return;
        }

        JsonDocument roadmapDoc;
        try
        {
            using var stream = File.OpenRead(roadmapJsonPath);
            roadmapDoc = JsonDocument.Parse(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // JSON validity is reported elsewhere (schema/parse validation).
            return;
        }

        using (roadmapDoc)
        {
            var root = roadmapDoc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (!root.TryGetProperty("roadmap", out var roadmap) || roadmap.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (!roadmap.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            for (var i = 0; i < items.GetArrayLength(); i++)
            {
                var item = items[i];
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var rawKind = TryGetStringProperty(item, "kind");
                var rawId = TryGetStringProperty(item, "id");

                if (!TryParseRoadmapItemKind(rawKind, out var expectedKind, out var kindError))
                {
                    issues.Add(new AosWorkspaceValidationIssue(
                        Layer: AosWorkspaceLayer.Spec,
                        ContractPath: ".aos/spec/roadmap.json",
                        Message: kindError,
                        InstanceLocation: $"/roadmap/items/{i}/kind"
                    ));
                    continue;
                }

                if (!AosPathRouter.TryParseArtifactId(rawId, out var parsedKind, out var normalizedId, out var idError))
                {
                    issues.Add(new AosWorkspaceValidationIssue(
                        Layer: AosWorkspaceLayer.Spec,
                        ContractPath: ".aos/spec/roadmap.json",
                        Message: idError,
                        InstanceLocation: $"/roadmap/items/{i}/id"
                    ));
                    continue;
                }

                if (parsedKind != expectedKind)
                {
                    issues.Add(new AosWorkspaceValidationIssue(
                        Layer: AosWorkspaceLayer.Spec,
                        ContractPath: ".aos/spec/roadmap.json",
                        Message: $"Roadmap item id '{rawId}' is kind '{parsedKind}', but kind is '{expectedKind}'.",
                        InstanceLocation: $"/roadmap/items/{i}"
                    ));
                    continue;
                }

                if (!string.Equals(normalizedId, (rawId ?? "").Trim(), StringComparison.Ordinal))
                {
                    issues.Add(new AosWorkspaceValidationIssue(
                        Layer: AosWorkspaceLayer.Spec,
                        ContractPath: ".aos/spec/roadmap.json",
                        Message: $"Roadmap item id '{rawId}' is not canonical; expected '{normalizedId}'.",
                        InstanceLocation: $"/roadmap/items/{i}/id"
                    ));
                    // Continue; we can still validate existence/index with the normalized id.
                }

                var contractPath = AosPathRouter.GetContractPath(expectedKind, normalizedId);
                var fullPath = AosPathRouter.ToAosRootPath(aosRootPath, contractPath);

                if (expectedKind == AosArtifactKind.Run)
                {
                    if (!Directory.Exists(fullPath))
                    {
                        issues.Add(new AosWorkspaceValidationIssue(
                            Layer: AosWorkspaceLayer.Spec,
                            ContractPath: ".aos/spec/roadmap.json",
                            Message: $"Roadmap item references missing artifact at '{contractPath}'.",
                            InstanceLocation: $"/roadmap/items/{i}/id"
                        ));
                        continue;
                    }
                }
                else
                {
                    if (Directory.Exists(fullPath))
                    {
                        issues.Add(new AosWorkspaceValidationIssue(
                            Layer: AosWorkspaceLayer.Spec,
                            ContractPath: ".aos/spec/roadmap.json",
                            Message: $"Roadmap item references '{contractPath}', but found a directory where a file is required.",
                            InstanceLocation: $"/roadmap/items/{i}/id"
                        ));
                        continue;
                    }

                    if (!File.Exists(fullPath))
                    {
                        issues.Add(new AosWorkspaceValidationIssue(
                            Layer: AosWorkspaceLayer.Spec,
                            ContractPath: ".aos/spec/roadmap.json",
                            Message: $"Roadmap item references missing artifact at '{contractPath}'.",
                            InstanceLocation: $"/roadmap/items/{i}/id"
                        ));
                        continue;
                    }
                }

                // If a catalog index exists for the kind, the referenced id MUST be present in the index.
                if (expectedKind == AosArtifactKind.Run)
                {
                    ValidateRunsIndexContains(aosRootPath, normalizedId, issues, instanceLocation: $"/roadmap/items/{i}/id");
                }
                else
                {
                    ValidateSpecCatalogIndexContains(aosRootPath, expectedKind, normalizedId, issues, instanceLocation: $"/roadmap/items/{i}/id");
                }
            }
        }
    }

    private static void ValidateStateCursorReference(string aosRootPath, List<AosWorkspaceValidationIssue> issues)
    {
        const string contractPath = ".aos/state/state.json";
        var stateJsonPath = Path.Combine(aosRootPath, "state", "state.json");
        if (!File.Exists(stateJsonPath) || Directory.Exists(stateJsonPath))
        {
            return;
        }

        JsonDocument stateDoc;
        try
        {
            using var stream = File.OpenRead(stateJsonPath);
            stateDoc = JsonDocument.Parse(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // JSON validity is reported elsewhere (schema/parse validation).
            return;
        }

        using (stateDoc)
        {
            var root = stateDoc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (!root.TryGetProperty("cursor", out var cursor) || cursor.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            var hasKindProp = cursor.TryGetProperty("kind", out var kindElement);
            var hasIdProp = cursor.TryGetProperty("id", out var idElement);

            // Cursor reference is optional; invariants only apply if kind/id are present.
            if (!hasKindProp && !hasIdProp)
            {
                return;
            }

            if (hasKindProp != hasIdProp)
            {
                issues.Add(new AosWorkspaceValidationIssue(
                    Layer: AosWorkspaceLayer.State,
                    ContractPath: contractPath,
                    Message: "Cursor reference is malformed: cursor.kind and cursor.id must either both be present or both be absent.",
                    InstanceLocation: hasKindProp ? "/cursor/id" : "/cursor/kind"
                ));
                return;
            }

            var rawKind = kindElement.ValueKind == JsonValueKind.String ? kindElement.GetString() : null;
            var rawId = idElement.ValueKind == JsonValueKind.String ? idElement.GetString() : null;

            if (!TryParseCursorKind(rawKind, out var expectedKind, out var kindError))
            {
                issues.Add(new AosWorkspaceValidationIssue(
                    Layer: AosWorkspaceLayer.State,
                    ContractPath: contractPath,
                    Message: kindError,
                    InstanceLocation: "/cursor/kind"
                ));
                return;
            }

            if (!AosPathRouter.TryParseArtifactId(rawId, out var parsedKind, out var normalizedId, out var idError))
            {
                issues.Add(new AosWorkspaceValidationIssue(
                    Layer: AosWorkspaceLayer.State,
                    ContractPath: contractPath,
                    Message: idError,
                    InstanceLocation: "/cursor/id"
                ));
                return;
            }

            if (parsedKind != expectedKind)
            {
                issues.Add(new AosWorkspaceValidationIssue(
                    Layer: AosWorkspaceLayer.State,
                    ContractPath: contractPath,
                    Message: $"Cursor id '{rawId}' is kind '{parsedKind}', but cursor.kind is '{expectedKind}'.",
                    InstanceLocation: "/cursor"
                ));
                return;
            }

            if (!string.Equals(normalizedId, (rawId ?? "").Trim(), StringComparison.Ordinal))
            {
                issues.Add(new AosWorkspaceValidationIssue(
                    Layer: AosWorkspaceLayer.State,
                    ContractPath: contractPath,
                    Message: $"Cursor id '{rawId}' is not canonical; expected '{normalizedId}'.",
                    InstanceLocation: "/cursor/id"
                ));
                // Continue; we can still validate existence/index with the normalized id.
            }

            var artifactContractPath = AosPathRouter.GetContractPath(expectedKind, normalizedId);
            var fullPath = AosPathRouter.ToAosRootPath(aosRootPath, artifactContractPath);

            if (expectedKind == AosArtifactKind.Run)
            {
                if (!Directory.Exists(fullPath))
                {
                    issues.Add(new AosWorkspaceValidationIssue(
                        Layer: AosWorkspaceLayer.State,
                        ContractPath: contractPath,
                        Message: $"Cursor references missing artifact at '{artifactContractPath}'.",
                        InstanceLocation: "/cursor/id"
                    ));
                    return;
                }
            }
            else
            {
                if (Directory.Exists(fullPath))
                {
                    issues.Add(new AosWorkspaceValidationIssue(
                        Layer: AosWorkspaceLayer.State,
                        ContractPath: contractPath,
                        Message: $"Cursor references '{artifactContractPath}', but found a directory where a file is required.",
                        InstanceLocation: "/cursor/id"
                    ));
                    return;
                }

                if (!File.Exists(fullPath))
                {
                    issues.Add(new AosWorkspaceValidationIssue(
                        Layer: AosWorkspaceLayer.State,
                        ContractPath: contractPath,
                        Message: $"Cursor references missing artifact at '{artifactContractPath}'.",
                        InstanceLocation: "/cursor/id"
                    ));
                    return;
                }
            }

            // If a catalog index exists for the kind, the referenced id MUST be present in the index.
            if (expectedKind == AosArtifactKind.Run)
            {
                ValidateRunsIndexContains(
                    aosRootPath,
                    normalizedRunId: normalizedId,
                    issues,
                    issueLayer: AosWorkspaceLayer.State,
                    issueContractPath: contractPath,
                    instanceLocation: "/cursor/id",
                    referenceLabel: "Cursor"
                );
            }
            else
            {
                ValidateSpecCatalogIndexContains(
                    aosRootPath,
                    expectedKind,
                    normalizedId,
                    issues,
                    issueLayer: AosWorkspaceLayer.State,
                    issueContractPath: contractPath,
                    instanceLocation: "/cursor/id",
                    referenceLabel: "Cursor"
                );
            }
        }
    }

    private static bool TryParseRoadmapItemKind(string? rawKind, out AosArtifactKind kind, out string error)
    {
        kind = default;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(rawKind))
        {
            error = "Missing roadmap item kind.";
            return false;
        }

        var normalized = rawKind.Trim().ToLowerInvariant();

        // Keep this tolerant: roadmap kind labels are stable strings aligned to routing rules,
        // but we accept a small set of human-friendly aliases (plural forms).
        kind = normalized switch
        {
            ArtifactKinds.Milestone or ArtifactKinds.Milestones => AosArtifactKind.Milestone,
            ArtifactKinds.Phase or ArtifactKinds.Phases => AosArtifactKind.Phase,
            ArtifactKinds.Task or ArtifactKinds.Tasks => AosArtifactKind.Task,
            ArtifactKinds.Issue or ArtifactKinds.Issues => AosArtifactKind.Issue,
            ArtifactKinds.Uat => AosArtifactKind.Uat,
            ArtifactKinds.Run or ArtifactKinds.Runs => AosArtifactKind.Run,
            _ => default
        };

        if (normalized is ArtifactKinds.Milestone or ArtifactKinds.Milestones or
            ArtifactKinds.Phase or ArtifactKinds.Phases or
            ArtifactKinds.Task or ArtifactKinds.Tasks or
            ArtifactKinds.Issue or ArtifactKinds.Issues or
            ArtifactKinds.Uat or
            ArtifactKinds.Run or ArtifactKinds.Runs)
        {
            return true;
        }

        error =
            $"Unrecognized roadmap item kind '{rawKind}'. Expected one of: " +
            $"{string.Join(", ", ArtifactKinds.CanonicalRoadmapItemKinds)}.";
        return false;
    }

    private static bool TryParseCursorKind(string? rawKind, out AosArtifactKind kind, out string error)
    {
        kind = default;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(rawKind))
        {
            error = "Missing cursor kind.";
            return false;
        }

        var normalized = rawKind.Trim().ToLowerInvariant();

        // Keep this tolerant: kind labels are stable strings aligned to routing rules,
        // but we accept a small set of human-friendly aliases (plural forms).
        kind = normalized switch
        {
            ArtifactKinds.Milestone or ArtifactKinds.Milestones => AosArtifactKind.Milestone,
            ArtifactKinds.Phase or ArtifactKinds.Phases => AosArtifactKind.Phase,
            ArtifactKinds.Task or ArtifactKinds.Tasks => AosArtifactKind.Task,
            ArtifactKinds.Issue or ArtifactKinds.Issues => AosArtifactKind.Issue,
            ArtifactKinds.Uat => AosArtifactKind.Uat,
            ArtifactKinds.Run or ArtifactKinds.Runs => AosArtifactKind.Run,
            _ => default
        };

        if (normalized is ArtifactKinds.Milestone or ArtifactKinds.Milestones or
            ArtifactKinds.Phase or ArtifactKinds.Phases or
            ArtifactKinds.Task or ArtifactKinds.Tasks or
            ArtifactKinds.Issue or ArtifactKinds.Issues or
            ArtifactKinds.Uat or
            ArtifactKinds.Run or ArtifactKinds.Runs)
        {
            return true;
        }

        error =
            $"Unrecognized cursor kind '{rawKind}'. Expected one of: " +
            $"{string.Join(", ", ArtifactKinds.CanonicalRoadmapItemKinds)}.";
        return false;
    }

    private static void ValidateSpecCatalogIndexContains(
        string aosRootPath,
        AosArtifactKind kind,
        string normalizedId,
        List<AosWorkspaceValidationIssue> issues,
        string instanceLocation)
    {
        ValidateSpecCatalogIndexContains(
            aosRootPath,
            kind,
            normalizedId,
            issues,
            issueLayer: AosWorkspaceLayer.Spec,
            issueContractPath: ".aos/spec/roadmap.json",
            instanceLocation: instanceLocation,
            referenceLabel: "Roadmap item"
        );
    }

    private static void ValidateSpecCatalogIndexContains(
        string aosRootPath,
        AosArtifactKind kind,
        string normalizedId,
        List<AosWorkspaceValidationIssue> issues,
        AosWorkspaceLayer issueLayer,
        string issueContractPath,
        string instanceLocation,
        string referenceLabel)
    {
        string indexContractPath;
        try
        {
            indexContractPath = nirmata.Aos.Engine.Stores.AosSpecStore.GetCatalogIndexContractPath(kind);
        }
        catch
        {
            // If the kind has no catalog index, there's nothing to enforce here.
            return;
        }

        var indexPath = AosPathRouter.ToAosRootPath(aosRootPath, indexContractPath);
        if (!File.Exists(indexPath) || Directory.Exists(indexPath))
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(indexPath);
            using var doc = JsonDocument.Parse(stream);

            if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var found = false;
            foreach (var entry in items.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.String &&
                    string.Equals(entry.GetString(), normalizedId, StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                issues.Add(new AosWorkspaceValidationIssue(
                    Layer: issueLayer,
                    ContractPath: issueContractPath,
                    Message: $"{referenceLabel} id '{normalizedId}' is not present in catalog index '{indexContractPath}'.",
                    InstanceLocation: instanceLocation
                ));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Index validity is reported elsewhere (schema/parse validation).
        }
    }

    private static void ValidateRunsIndexContains(
        string aosRootPath,
        string normalizedRunId,
        List<AosWorkspaceValidationIssue> issues,
        string instanceLocation)
    {
        ValidateRunsIndexContains(
            aosRootPath,
            normalizedRunId,
            issues,
            issueLayer: AosWorkspaceLayer.Spec,
            issueContractPath: ".aos/spec/roadmap.json",
            instanceLocation: instanceLocation,
            referenceLabel: "Roadmap item"
        );
    }

    private static void ValidateRunsIndexContains(
        string aosRootPath,
        string normalizedRunId,
        List<AosWorkspaceValidationIssue> issues,
        AosWorkspaceLayer issueLayer,
        string issueContractPath,
        string instanceLocation,
        string referenceLabel)
    {
        const string indexContractPath = ".aos/evidence/runs/index.json";
        var indexPath = Path.Combine(aosRootPath, "evidence", "runs", "index.json");
        if (!File.Exists(indexPath) || Directory.Exists(indexPath))
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(indexPath);
            using var doc = JsonDocument.Parse(stream);

            if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var found = false;
            foreach (var entry in items.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var runId = TryGetStringProperty(entry, "runId");
                if (string.Equals(runId, normalizedRunId, StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                issues.Add(new AosWorkspaceValidationIssue(
                    Layer: issueLayer,
                    ContractPath: issueContractPath,
                    Message: $"{referenceLabel} id '{normalizedRunId}' is not present in runs index '{indexContractPath}'.",
                    InstanceLocation: instanceLocation
                ));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Index validity is reported elsewhere (schema/parse validation).
        }
    }

}

internal sealed record AosWorkspaceValidationReport(
    string RepositoryRootPath,
    string AosRootPath,
    IReadOnlyList<AosWorkspaceLayer> Layers,
    IReadOnlyList<AosWorkspaceValidationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

internal sealed record AosWorkspaceValidationIssue(
    AosWorkspaceLayer? Layer,
    string ContractPath,
    string Message,
    string? SchemaId = null,
    string? InstanceLocation = null
);

