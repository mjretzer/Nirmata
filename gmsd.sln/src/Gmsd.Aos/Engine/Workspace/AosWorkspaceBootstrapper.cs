using System.Text.Json;
using System.Text.Json.Serialization;
using Gmsd.Aos.Engine;
using Gmsd.Aos.Engine.Locks;
using Gmsd.Aos.Engine.Paths;
using Gmsd.Aos.Engine.State;
using Gmsd.Aos.Engine.StateTransitions;
using Gmsd.Aos.Engine.Stores;
using Gmsd.Aos.Engine.Spec;
using Gmsd.Aos.Engine.Schemas;

namespace Gmsd.Aos.Engine.Workspace;

internal static class AosWorkspaceBootstrapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly string[] CanonicalTopLevelDirectories =
    [
        "spec",
        "state",
        "evidence",
        "context",
        "codebase",
        "cache",
        "config",
        "schemas",
        "locks"
    ];

    public static AosWorkspaceBootstrapResult EnsureInitialized(string repositoryRootPath)
    {
        if (repositoryRootPath is null) throw new ArgumentNullException(nameof(repositoryRootPath));

        var aosRootPath = Path.Combine(repositoryRootPath, ".aos");

        if (File.Exists(aosRootPath))
        {
            throw new AosWorkspaceNonCompliantException(
                aosRootPath,
                AosWorkspaceComplianceReport.NonCompliantAosRootIsFile(aosRootPath)
            );
        }

        if (!Directory.Exists(aosRootPath))
        {
            BootstrapNewWorkspace(aosRootPath);
            return AosWorkspaceBootstrapResult.Created(aosRootPath);
        }

        // Ensure idempotent "repair" behavior: init SHOULD be able to seed missing
        // baseline artifacts and directories without overwriting existing files.
        BootstrapNewWorkspace(aosRootPath);

        var compliance = CheckCompliance(aosRootPath);
        if (!compliance.IsCompliant)
        {
            throw new AosWorkspaceNonCompliantException(aosRootPath, compliance);
        }

        return AosWorkspaceBootstrapResult.NoChanges(aosRootPath);
    }

    public static AosWorkspaceRepairResult Repair(string repositoryRootPath)
    {
        if (repositoryRootPath is null) throw new ArgumentNullException(nameof(repositoryRootPath));

        var aosRootPath = Path.Combine(repositoryRootPath, ".aos");
        var repairStartTime = DateTimeOffset.UtcNow;

        var lockResult = AosWorkspaceLockManager.TryAcquireExclusive(aosRootPath, "repair", createDirectories: true);
        if (!lockResult.Acquired)
        {
            return AosWorkspaceRepairResult.FailedToAcquireLock(aosRootPath, lockResult.Message);
        }

        try
        {
            using var lockHandle = lockResult.Handle;

            // Re-run initialization to seed missing files
            BootstrapNewWorkspace(aosRootPath);

            // Rebuild all index.json files by scanning directories
            RebuildIndexFiles(aosRootPath);

            // Validate all JSON artifacts against schemas
            var schemaValidationIssues = ValidateArtifactSchemas(aosRootPath);

            var compliance = CheckCompliance(aosRootPath);
            if (!compliance.IsCompliant)
            {
                return AosWorkspaceRepairResult.FailedComplianceCheck(aosRootPath, compliance);
            }

            var repairDuration = DateTimeOffset.UtcNow - repairStartTime;
            return AosWorkspaceRepairResult.Success(aosRootPath, schemaValidationIssues, repairDuration);
        }
        catch (Exception ex)
        {
            return AosWorkspaceRepairResult.FailedToAcquireLock(aosRootPath, ex.Message);
        }
    }

    private static void RebuildIndexFiles(string aosRootPath)
    {
        var catalogDirs = new[] { "milestones", "phases", "tasks", "issues", "uat" };

        foreach (var catalogDir in catalogDirs)
        {
            var catalogPath = Path.Combine(aosRootPath, "spec", catalogDir);
            var indexPath = Path.Combine(catalogPath, "index.json");

            if (!Directory.Exists(catalogPath))
            {
                continue;
            }

            var items = new List<CatalogIndexItem>();
            foreach (var file in Directory.EnumerateFiles(catalogPath, "*.json"))
            {
                var fileName = Path.GetFileName(file);
                if (fileName == "index.json")
                {
                    continue;
                }

                try
                {
                    using var stream = File.OpenRead(file);
                    using var doc = JsonDocument.Parse(stream);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String)
                    {
                        var id = idElement.GetString();
                        items.Add(new CatalogIndexItem(Id: id ?? string.Empty, Path: fileName));
                    }
                }
                catch
                {
                    // Skip files that cannot be parsed
                }
            }

            var index = new CatalogIndexDocument(SchemaVersion: 1, Items: items);
            DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(indexPath, index, JsonOptions);
        }
    }

    private static List<string> ValidateArtifactSchemas(string aosRootPath)
    {
        var issues = new List<string>();

        var specPath = Path.Combine(aosRootPath, "spec");
        if (!Directory.Exists(specPath))
        {
            return issues;
        }

        foreach (var file in Directory.EnumerateFiles(specPath, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                using var stream = File.OpenRead(file);
                using var doc = JsonDocument.Parse(stream);
                var root = doc.RootElement;

                if (root.TryGetProperty("schemaVersion", out var versionElement))
                {
                    if (versionElement.ValueKind != JsonValueKind.Number)
                    {
                        issues.Add($"{file}: Invalid schemaVersion (expected number)");
                    }
                }
                else
                {
                    issues.Add($"{file}: Missing schemaVersion");
                }
            }
            catch (JsonException ex)
            {
                issues.Add($"{file}: Invalid JSON - {ex.Message}");
            }
            catch (Exception ex)
            {
                issues.Add($"{file}: Error reading file - {ex.Message}");
            }
        }

        return issues;
    }

    public static AosWorkspaceComplianceReport CheckCompliance(string repositoryRootPath, bool treatMissingAosRootAsNonCompliant = true)
    {
        if (repositoryRootPath is null) throw new ArgumentNullException(nameof(repositoryRootPath));

        var aosRootPath = Path.Combine(repositoryRootPath, ".aos");
        if (File.Exists(aosRootPath))
        {
            return AosWorkspaceComplianceReport.NonCompliantAosRootIsFile(aosRootPath);
        }

        if (!Directory.Exists(aosRootPath))
        {
            return treatMissingAosRootAsNonCompliant
                ? AosWorkspaceComplianceReport.NonCompliantMissingAosRoot(aosRootPath)
                : AosWorkspaceComplianceReport.CompliantDoesNotExist(aosRootPath);
        }

        return CheckCompliance(aosRootPath);
    }

    private static void BootstrapNewWorkspace(string aosRootPath)
    {
        // Canonical top-level directories
        foreach (var dir in CanonicalTopLevelDirectories)
        {
            Directory.CreateDirectory(Path.Combine(aosRootPath, dir));
        }

        // Additional required subfolders for baseline artifacts
        Directory.CreateDirectory(Path.Combine(aosRootPath, "spec", "milestones"));
        Directory.CreateDirectory(Path.Combine(aosRootPath, "spec", "phases"));
        Directory.CreateDirectory(Path.Combine(aosRootPath, "spec", "tasks"));
        Directory.CreateDirectory(Path.Combine(aosRootPath, "spec", "issues"));
        Directory.CreateDirectory(Path.Combine(aosRootPath, "spec", "uat"));
        Directory.CreateDirectory(Path.Combine(aosRootPath, "evidence", "logs"));
        Directory.CreateDirectory(Path.Combine(aosRootPath, "evidence", "runs"));

        DeterministicJsonFileWriter.WriteCanonicalJsonIfMissing(
            AosPathRouter.ToAosRootPath(aosRootPath, ".aos/spec/project.json"),
            new ProjectSpecDocument(
                SchemaVersion: 1,
                Project: new ProjectSpec(Name: "", Description: "")
            ),
            JsonOptions
        );

        DeterministicJsonFileWriter.WriteCanonicalJsonIfMissing(
            AosPathRouter.ToAosRootPath(aosRootPath, ".aos/spec/roadmap.json"),
            new RoadmapSpecDocument(
                SchemaVersion: 1,
                Roadmap: new RoadmapSpec(Title: "", Items: [])
            ),
            JsonOptions
        );

        DeterministicJsonFileWriter.WriteCanonicalJsonIfMissing(
            AosPathRouter.ToAosRootPath(aosRootPath, ".aos/spec/milestones/index.json"),
            new CatalogIndexDocument(SchemaVersion: 1, Items: []),
            JsonOptions
        );

        DeterministicJsonFileWriter.WriteCanonicalJsonIfMissing(
            AosPathRouter.ToAosRootPath(aosRootPath, ".aos/spec/phases/index.json"),
            new CatalogIndexDocument(SchemaVersion: 1, Items: []),
            JsonOptions
        );

        DeterministicJsonFileWriter.WriteCanonicalJsonIfMissing(
            AosPathRouter.ToAosRootPath(aosRootPath, ".aos/spec/tasks/index.json"),
            new CatalogIndexDocument(SchemaVersion: 1, Items: []),
            JsonOptions
        );

        DeterministicJsonFileWriter.WriteCanonicalJsonIfMissing(
            AosPathRouter.ToAosRootPath(aosRootPath, ".aos/spec/issues/index.json"),
            new CatalogIndexDocument(SchemaVersion: 1, Items: []),
            JsonOptions
        );

        DeterministicJsonFileWriter.WriteCanonicalJsonIfMissing(
            AosPathRouter.ToAosRootPath(aosRootPath, ".aos/spec/uat/index.json"),
            new CatalogIndexDocument(SchemaVersion: 1, Items: []),
            JsonOptions
        );

        // Baseline state artifacts (snapshot + append-only events log), gated by a validated transition.
        AosStateTransitionEngine.EnsureStateInitialized(aosRootPath);

        // Baseline evidence artifacts (command log + run index).
        DeterministicJsonFileWriter.WriteCanonicalJsonIfMissing(
            AosPathRouter.ToAosRootPath(aosRootPath, ".aos/evidence/logs/commands.json"),
            new { SchemaVersion = 1, Items = Array.Empty<object>() },
            JsonOptions
        );

        DeterministicJsonFileWriter.WriteCanonicalJsonIfMissing(
            AosPathRouter.ToAosRootPath(aosRootPath, ".aos/evidence/runs/index.json"),
            new { SchemaVersion = 1, Items = Array.Empty<object>() },
            JsonOptions
        );

        DeterministicJsonFileWriter.WriteCanonicalJsonIfMissing(
            AosPathRouter.ToAosRootPath(aosRootPath, ".aos/schemas/registry.json"),
            new SchemaRegistryDocument(
                SchemaVersion: 1,
                Schemas: AosEmbeddedSchemaRegistryLoader
                    .LoadEmbeddedSchemas()
                    .Select(static s => s.FileName)
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
                ArtifactContracts: ArtifactContractSchemaCatalog.RequiredContracts
                    .Select(static c => new ArtifactContractRegistryEntry(
                        SchemaId: c.SchemaId,
                        CurrentVersion: c.CurrentVersion,
                        SupportedVersions: c.SupportedVersions,
                        DeprecatedVersions: c.DeprecatedVersions))
                    .OrderBy(static c => c.SchemaId, StringComparer.Ordinal)
                    .ToArray()
            ),
            JsonOptions
        );

        // Seed a baseline policy for safe, explicit execution gates.
        // (Policy is required by execute-plan and future agent execution entrypoints.)
        DeterministicJsonFileWriter.WriteCanonicalJsonIfMissing(
            AosPathRouter.ToAosRootPath(aosRootPath, ".aos/config/policy.json"),
            new
            {
                SchemaVersion = 1,
                ScopeAllowlist = new
                {
                    // Default: allow engine-owned writes anywhere under .aos/.
                    // More restrictive policies can narrow this to specific subpaths.
                    Write = new[] { ".aos/" }
                },
                ToolAllowlist = new
                {
                    Tools = Array.Empty<string>(),
                    Providers = Array.Empty<string>()
                },
                NoImplicitState = true
            },
            JsonOptions
        );

        // Seed local schema pack templates deterministically from engine-owned embedded templates.
        var schemasRootPath = Path.Combine(aosRootPath, "schemas");
        foreach (var schema in AosEmbeddedSchemaRegistryLoader.LoadEmbeddedSchemas())
        {
            DeterministicJsonFileWriter.WriteCanonicalJsonTextIfMissing(
                Path.Combine(schemasRootPath, schema.FileName),
                schema.Json
            );
        }

        // Seed workspace-specific config if missing
        DeterministicJsonFileWriter.WriteCanonicalJsonIfMissing(
            Path.Combine(aosRootPath, "config", "workspace.json"),
            new AosWorkspaceConfigDocument(
                SchemaVersion: 1,
                AgentPreferences: new Dictionary<string, object>(),
                EngineOverrides: new Dictionary<string, object>(),
                ExcludedPaths: []
            ),
            JsonOptions
        );
    }

    public static AosWorkspaceConfigDocument? ReadWorkspaceConfig(string repositoryRootPath)
    {
        if (repositoryRootPath is null) throw new ArgumentNullException(nameof(repositoryRootPath));

        var aosRootPath = Path.Combine(repositoryRootPath, ".aos");
        var configPath = Path.Combine(aosRootPath, "config", "workspace.json");

        if (!File.Exists(configPath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(configPath);
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;

            if (!root.TryGetProperty("schemaVersion", out var versionElement) || versionElement.ValueKind != JsonValueKind.Number)
            {
                return null;
            }

            var agentPrefs = new Dictionary<string, object>();
            if (root.TryGetProperty("agentPreferences", out var agentPrefsElement) && agentPrefsElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in agentPrefsElement.EnumerateObject())
                {
                    agentPrefs[prop.Name] = prop.Value.GetRawText();
                }
            }

            var engineOverrides = new Dictionary<string, object>();
            if (root.TryGetProperty("engineOverrides", out var engineOverridesElement) && engineOverridesElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in engineOverridesElement.EnumerateObject())
                {
                    engineOverrides[prop.Name] = prop.Value.GetRawText();
                }
            }

            var excludedPaths = new List<string>();
            if (root.TryGetProperty("excludedPaths", out var excludedPathsElement) && excludedPathsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in excludedPathsElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var path = item.GetString();
                        if (path != null)
                        {
                            excludedPaths.Add(path);
                        }
                    }
                }
            }

            return new AosWorkspaceConfigDocument(
                SchemaVersion: versionElement.GetInt32(),
                AgentPreferences: agentPrefs,
                EngineOverrides: engineOverrides,
                ExcludedPaths: excludedPaths
            );
        }
        catch
        {
            return null;
        }
    }

    public static bool WriteWorkspaceConfig(string repositoryRootPath, AosWorkspaceConfigDocument config)
    {
        if (repositoryRootPath is null) throw new ArgumentNullException(nameof(repositoryRootPath));
        if (config is null) throw new ArgumentNullException(nameof(config));

        var aosRootPath = Path.Combine(repositoryRootPath, ".aos");
        var configPath = Path.Combine(aosRootPath, "config", "workspace.json");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(configPath, config, JsonOptions);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static AosWorkspaceComplianceReport CheckCompliance(string aosRootPath)
    {
        var missingDirectories = new List<string>();
        var invalidDirectories = new List<string>();
        var extraTopLevelEntries = new List<string>();
        var missingFiles = new List<string>();
        var invalidFiles = new List<string>();

        var expectedTopLevel = new HashSet<string>(CanonicalTopLevelDirectories, StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(aosRootPath))
        {
            if (File.Exists(aosRootPath))
            {
                return AosWorkspaceComplianceReport.NonCompliantAosRootIsFile(aosRootPath);
            }

            return AosWorkspaceComplianceReport.NonCompliantMissingAosRoot(aosRootPath);
        }

        foreach (var dirName in CanonicalTopLevelDirectories)
        {
            var fullPath = Path.Combine(aosRootPath, dirName);
            if (!Directory.Exists(fullPath))
            {
                if (File.Exists(fullPath))
                {
                    invalidDirectories.Add($".aos/{dirName} (expected directory, found file)");
                }
                else
                {
                    missingDirectories.Add($".aos/{dirName}");
                }
            }
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(aosRootPath))
        {
            var name = Path.GetFileName(entry);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (!expectedTopLevel.Contains(name))
            {
                extraTopLevelEntries.Add($".aos/{name}");
            }
        }

        ValidateRequiredJsonFile(".aos/spec/project.json", AosPathRouter.ToAosRootPath(aosRootPath, ".aos/spec/project.json"), missingFiles, invalidFiles);
        ValidateRequiredJsonFile(".aos/spec/roadmap.json", AosPathRouter.ToAosRootPath(aosRootPath, ".aos/spec/roadmap.json"), missingFiles, invalidFiles);
        ValidateRequiredJsonFile(".aos/spec/milestones/index.json", AosPathRouter.ToAosRootPath(aosRootPath, ".aos/spec/milestones/index.json"), missingFiles, invalidFiles);
        ValidateRequiredJsonFile(".aos/spec/phases/index.json", AosPathRouter.ToAosRootPath(aosRootPath, ".aos/spec/phases/index.json"), missingFiles, invalidFiles);
        ValidateRequiredJsonFile(".aos/spec/tasks/index.json", AosPathRouter.ToAosRootPath(aosRootPath, ".aos/spec/tasks/index.json"), missingFiles, invalidFiles);
        ValidateRequiredJsonFile(".aos/spec/issues/index.json", AosPathRouter.ToAosRootPath(aosRootPath, ".aos/spec/issues/index.json"), missingFiles, invalidFiles);
        ValidateRequiredJsonFile(".aos/spec/uat/index.json", AosPathRouter.ToAosRootPath(aosRootPath, ".aos/spec/uat/index.json"), missingFiles, invalidFiles);
        ValidateRequiredJsonFile(".aos/state/state.json", AosPathRouter.ToAosRootPath(aosRootPath, ".aos/state/state.json"), missingFiles, invalidFiles);
        ValidateRequiredFile(".aos/state/events.ndjson", AosPathRouter.ToAosRootPath(aosRootPath, ".aos/state/events.ndjson"), missingFiles, invalidFiles);
        ValidateRequiredJsonFile(".aos/evidence/logs/commands.json", AosPathRouter.ToAosRootPath(aosRootPath, ".aos/evidence/logs/commands.json"), missingFiles, invalidFiles);
        ValidateRequiredJsonFile(".aos/evidence/runs/index.json", AosPathRouter.ToAosRootPath(aosRootPath, ".aos/evidence/runs/index.json"), missingFiles, invalidFiles);
        ValidateRequiredJsonFile(".aos/schemas/registry.json", AosPathRouter.ToAosRootPath(aosRootPath, ".aos/schemas/registry.json"), missingFiles, invalidFiles);

        // Schema-aware validation: check lock file status if present
        ValidateWorkspaceLockIfPresent(aosRootPath, invalidFiles);

        return AosWorkspaceComplianceReport.FromChecks(
            aosRootPath,
            missingDirectories,
            invalidDirectories,
            missingFiles,
            invalidFiles,
            extraTopLevelEntries
        );
    }

    private static void ValidateWorkspaceLockIfPresent(string aosRootPath, List<string> invalidFiles)
    {
        var lockPath = AosPathRouter.GetWorkspaceLockPath(aosRootPath);
        if (!File.Exists(lockPath))
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(lockPath);
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;

            if (!root.TryGetProperty("schemaVersion", out var versionElement) || versionElement.ValueKind != JsonValueKind.Number)
            {
                invalidFiles.Add($".aos/locks/workspace.lock.json (missing or invalid schemaVersion)");
            }
        }
        catch (JsonException)
        {
            invalidFiles.Add($".aos/locks/workspace.lock.json (invalid JSON)");
        }
        catch
        {
            invalidFiles.Add($".aos/locks/workspace.lock.json (cannot be read)");
        }
    }

    private static void ValidateRequiredJsonFile(
        string contractPath,
        string filePath,
        List<string> missingFiles,
        List<string> invalidFiles)
    {
        if (File.Exists(filePath))
        {
            if (Directory.Exists(filePath))
            {
                invalidFiles.Add($"{contractPath} (expected file, found directory)");
                return;
            }

            try
            {
                using var stream = File.OpenRead(filePath);
                using var _ = JsonDocument.Parse(stream);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                invalidFiles.Add($"{contractPath} (invalid JSON)");
            }

            return;
        }

        if (Directory.Exists(filePath))
        {
            invalidFiles.Add($"{contractPath} (expected file, found directory)");
            return;
        }

        missingFiles.Add(contractPath);
    }

    private static void ValidateRequiredFile(
        string contractPath,
        string filePath,
        List<string> missingFiles,
        List<string> invalidFiles)
    {
        if (File.Exists(filePath))
        {
            if (Directory.Exists(filePath))
            {
                invalidFiles.Add($"{contractPath} (expected file, found directory)");
            }

            return;
        }

        if (Directory.Exists(filePath))
        {
            invalidFiles.Add($"{contractPath} (expected file, found directory)");
            return;
        }

        missingFiles.Add(contractPath);
    }

    private sealed record ArtifactContractRegistryEntry(
        string SchemaId,
        int CurrentVersion,
        IReadOnlyList<int> SupportedVersions,
        IReadOnlyList<int> DeprecatedVersions);

    private sealed record SchemaRegistryDocument(
        int SchemaVersion,
        IReadOnlyList<string> Schemas,
        IReadOnlyList<ArtifactContractRegistryEntry> ArtifactContracts);

    private sealed record CatalogIndexItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("path")] string Path);

    private sealed record CatalogIndexDocument(
        [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
        [property: JsonPropertyName("items")] IReadOnlyList<CatalogIndexItem> Items);
}

