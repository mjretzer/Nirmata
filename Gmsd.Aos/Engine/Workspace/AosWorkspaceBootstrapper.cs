using System.Text.Json;
using Gmsd.Aos.Engine;
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

        return AosWorkspaceComplianceReport.FromChecks(
            aosRootPath,
            missingDirectories,
            invalidDirectories,
            missingFiles,
            invalidFiles,
            extraTopLevelEntries
        );
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

    private sealed record SchemaRegistryDocument(int SchemaVersion, IReadOnlyList<string> Schemas);
}

