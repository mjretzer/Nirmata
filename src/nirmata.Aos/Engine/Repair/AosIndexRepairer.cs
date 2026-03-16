using System.Text.Json;
using nirmata.Aos.Engine.Paths;
using nirmata.Aos.Engine.Spec;
using nirmata.Aos.Engine.Stores;

namespace nirmata.Aos.Engine.Repair;

internal static class AosIndexRepairer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static AosIndexRepairResult RepairIndexes(string aosRootPath)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));

        var issues = new List<AosIndexRepairIssue>();

        if (File.Exists(aosRootPath))
        {
            issues.Add(
                new AosIndexRepairIssue(
                    ContractPath: ".aos/",
                    Message: $"Expected directory, found file at '{aosRootPath}'.",
                    SuggestedFix: "Delete/rename the file and re-run 'aos init'."
                )
            );
            throw new AosIndexRepairFailedException(issues);
        }

        if (!Directory.Exists(aosRootPath))
        {
            issues.Add(
                new AosIndexRepairIssue(
                    ContractPath: ".aos/",
                    Message: $"Missing AOS workspace root directory at '{aosRootPath}'.",
                    SuggestedFix: "Run 'aos init' to create the baseline workspace, then re-run the repair."
                )
            );
            throw new AosIndexRepairFailedException(issues);
        }

        var specStore = new AosSpecStore(aosRootPath);

        var specCounts = new Dictionary<AosArtifactKind, int>();

        // Rebuild spec catalog indexes deterministically from disk state.
        specCounts[AosArtifactKind.Milestone] = RebuildSpecCatalogIndex(
            aosRootPath,
            specStore,
            AosArtifactKind.Milestone,
            folderName: "milestones",
            artifactFileName: "milestone.json",
            issues: issues
        );

        specCounts[AosArtifactKind.Phase] = RebuildSpecCatalogIndex(
            aosRootPath,
            specStore,
            AosArtifactKind.Phase,
            folderName: "phases",
            artifactFileName: "phase.json",
            issues: issues
        );

        specCounts[AosArtifactKind.Task] = RebuildSpecCatalogIndex(
            aosRootPath,
            specStore,
            AosArtifactKind.Task,
            folderName: "tasks",
            artifactFileName: "task.json",
            issues: issues
        );

        specCounts[AosArtifactKind.Issue] = RebuildSpecCatalogIndexFromFlatJsonArtifacts(
            aosRootPath,
            specStore,
            AosArtifactKind.Issue,
            folderName: "issues",
            issues: issues
        );

        specCounts[AosArtifactKind.Uat] = RebuildSpecCatalogIndexFromFlatJsonArtifacts(
            aosRootPath,
            specStore,
            AosArtifactKind.Uat,
            folderName: "uat",
            issues: issues
        );

        // Rebuild run index deterministically from run metadata on disk.
        var runCount = RebuildRunsIndex(aosRootPath, issues);

        if (issues.Count > 0)
        {
            throw new AosIndexRepairFailedException(issues);
        }

        return new AosIndexRepairResult(
            AosRootPath: aosRootPath,
            SpecCatalogCounts: specCounts,
            RunCount: runCount
        );
    }

    private static int RebuildSpecCatalogIndex(
        string aosRootPath,
        AosSpecStore store,
        AosArtifactKind kind,
        string folderName,
        string artifactFileName,
        List<AosIndexRepairIssue> issues)
    {
        var rootPath = Path.Combine(aosRootPath, "spec", folderName);

        var discovered = new HashSet<string>(StringComparer.Ordinal);

        if (Directory.Exists(rootPath))
        {
            foreach (var fullDir in Directory.EnumerateDirectories(rootPath))
            {
                var dirName = Path.GetFileName(fullDir);
                if (string.IsNullOrWhiteSpace(dirName))
                {
                    continue;
                }

                if (!AosPathRouter.TryParseArtifactId(dirName, out var parsedKind, out var normalizedId, out var error))
                {
                    // Ignore non-artifact directories (repair only cares about contract-shaped artifacts).
                    continue;
                }

                if (parsedKind != kind)
                {
                    issues.Add(
                        new AosIndexRepairIssue(
                            ContractPath: $".aos/spec/{folderName}/{dirName}/",
                            Message: $"Artifact id '{dirName}' is kind '{parsedKind}', expected '{kind}'.",
                            SuggestedFix: $"Move the directory to the correct kind folder or rename/delete it, then re-run 'aos repair indexes'."
                        )
                    );
                    continue;
                }

                if (!string.Equals(normalizedId, dirName, StringComparison.Ordinal))
                {
                    issues.Add(
                        new AosIndexRepairIssue(
                            ContractPath: $".aos/spec/{folderName}/{dirName}/",
                            Message: $"Artifact directory name '{dirName}' is not canonical for id '{normalizedId}'.",
                            SuggestedFix: $"Rename the directory to '{normalizedId}' (exact match), then re-run 'aos repair indexes'."
                        )
                    );
                    continue;
                }

                var artifactPath = Path.Combine(fullDir, artifactFileName);
                if (!File.Exists(artifactPath))
                {
                    if (Directory.Exists(artifactPath))
                    {
                        issues.Add(
                            new AosIndexRepairIssue(
                                ContractPath: $".aos/spec/{folderName}/{dirName}/{artifactFileName}",
                                Message: "Expected file, found directory.",
                                SuggestedFix: "Replace it with the correct JSON artifact file, or delete the invalid directory and re-run the repair."
                            )
                        );
                        continue;
                    }

                    issues.Add(
                        new AosIndexRepairIssue(
                            ContractPath: $".aos/spec/{folderName}/{dirName}/{artifactFileName}",
                            Message: "Missing required artifact file for an existing artifact directory.",
                            SuggestedFix: "Restore the missing JSON file or delete the incomplete artifact directory, then re-run 'aos repair indexes'."
                        )
                    );
                    continue;
                }

                discovered.Add(normalizedId);
            }
        }

        var items = discovered.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        store.WriteCatalogIndexOverwrite(kind, new CatalogIndexDocument(SchemaVersion: 1, Items: items));
        return items.Length;
    }

    private static int RebuildSpecCatalogIndexFromFlatJsonArtifacts(
        string aosRootPath,
        AosSpecStore store,
        AosArtifactKind kind,
        string folderName,
        List<AosIndexRepairIssue> issues)
    {
        var rootPath = Path.Combine(aosRootPath, "spec", folderName);

        var discovered = new HashSet<string>(StringComparer.Ordinal);

        if (Directory.Exists(rootPath))
        {
            foreach (var fullFilePath in Directory.EnumerateFiles(rootPath, "*.json"))
            {
                var fileName = Path.GetFileName(fullFilePath);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                // Skip the catalog index itself.
                if (string.Equals(fileName, "index.json", StringComparison.Ordinal))
                {
                    continue;
                }

                var baseName = Path.GetFileNameWithoutExtension(fileName);
                if (string.IsNullOrWhiteSpace(baseName))
                {
                    continue;
                }

                if (!AosPathRouter.TryParseArtifactId(baseName, out var parsedKind, out var normalizedId, out var error))
                {
                    // Surface actionable errors for "close but wrong" ids (case-insensitive prefix match).
                    var expectedPrefix = kind switch
                    {
                        AosArtifactKind.Issue => "ISS-",
                        AosArtifactKind.Uat => "UAT-",
                        _ => null
                    };

                    if (expectedPrefix is not null &&
                        baseName.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(
                            new AosIndexRepairIssue(
                                ContractPath: $".aos/spec/{folderName}/{fileName}",
                                Message: error,
                                SuggestedFix: $"Rename the file to use a canonical {expectedPrefix}#### id (e.g. {expectedPrefix}0001.json), or delete it if it is not a {kind} artifact."
                            )
                        );
                    }

                    // Ignore unrelated JSON files.
                    continue;
                }

                if (parsedKind != kind)
                {
                    issues.Add(
                        new AosIndexRepairIssue(
                            ContractPath: $".aos/spec/{folderName}/{fileName}",
                            Message: $"Artifact id '{baseName}' is kind '{parsedKind}', expected '{kind}'.",
                            SuggestedFix: "Move the file to the correct kind folder or rename/delete it, then re-run 'aos repair indexes'."
                        )
                    );
                    continue;
                }

                if (!string.Equals(normalizedId, baseName, StringComparison.Ordinal))
                {
                    issues.Add(
                        new AosIndexRepairIssue(
                            ContractPath: $".aos/spec/{folderName}/{fileName}",
                            Message: $"Artifact file name '{baseName}.json' is not canonical for id '{normalizedId}'.",
                            SuggestedFix: $"Rename the file to '{normalizedId}.json' (exact match), then re-run 'aos repair indexes'."
                        )
                    );
                    continue;
                }

                discovered.Add(normalizedId);
            }
        }

        var items = discovered.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        store.WriteCatalogIndexOverwrite(kind, new CatalogIndexDocument(SchemaVersion: 1, Items: items));
        return items.Length;
    }

    private static int RebuildRunsIndex(string aosRootPath, List<AosIndexRepairIssue> issues)
    {
        var runsRootPath = Path.Combine(aosRootPath, "evidence", "runs");
        var runsIndexPath = AosPathRouter.GetRunsIndexPath(aosRootPath);

        var items = new List<RunIndexItemDocument>();

        if (Directory.Exists(runsRootPath))
        {
            foreach (var fullDir in Directory.EnumerateDirectories(runsRootPath))
            {
                var dirName = Path.GetFileName(fullDir);
                if (string.IsNullOrWhiteSpace(dirName))
                {
                    continue;
                }

                // Ignore non-run directories unless they look like a (possibly malformed) run id.
                if (!AosPathRouter.TryParseArtifactId(dirName, out var kind, out var normalizedId, out var error))
                {
                    // If it *looks* like a run id but isn't canonical, surface an actionable error.
                    if (LooksLikeHex32(dirName))
                    {
                        issues.Add(
                            new AosIndexRepairIssue(
                                ContractPath: $".aos/evidence/runs/{dirName}/",
                                Message: error,
                                SuggestedFix: "Rename the run directory to the canonical run id (32 lower-case hex), or delete it if it is not a run."
                            )
                        );
                    }

                    continue;
                }

                if (kind != AosArtifactKind.Run)
                {
                    // Non-run artifact ids shouldn't appear under runs/.
                    issues.Add(
                        new AosIndexRepairIssue(
                            ContractPath: $".aos/evidence/runs/{dirName}/",
                            Message: $"Artifact id '{dirName}' is kind '{kind}', expected '{AosArtifactKind.Run}'.",
                            SuggestedFix: "Move the directory out of '.aos/evidence/runs/' or delete it, then re-run the repair."
                        )
                    );
                    continue;
                }

                if (!string.Equals(normalizedId, dirName, StringComparison.Ordinal))
                {
                    issues.Add(
                        new AosIndexRepairIssue(
                            ContractPath: $".aos/evidence/runs/{dirName}/",
                            Message: $"Run directory name '{dirName}' is not canonical for id '{normalizedId}'.",
                            SuggestedFix: $"Rename the directory to '{normalizedId}' (exact match), then re-run 'aos repair indexes'."
                        )
                    );
                    continue;
                }

                var canonicalRunJsonPath = AosPathRouter.GetRunMetadataPath(aosRootPath, dirName);
                var legacyRunJsonPath = AosPathRouter.GetLegacyRunMetadataPath(aosRootPath, dirName);
                var runJsonPath = File.Exists(canonicalRunJsonPath) ? canonicalRunJsonPath : legacyRunJsonPath;

                if (!File.Exists(runJsonPath))
                {
                    if (Directory.Exists(canonicalRunJsonPath) || Directory.Exists(legacyRunJsonPath))
                    {
                        issues.Add(
                            new AosIndexRepairIssue(
                                ContractPath: $".aos/evidence/runs/{dirName}/artifacts/run.json",
                                Message: "Expected file, found directory.",
                                SuggestedFix: "Replace it with a valid run.json file, or delete the invalid run directory."
                            )
                        );
                    }
                    else
                    {
                        issues.Add(
                            new AosIndexRepairIssue(
                                ContractPath: $".aos/evidence/runs/{dirName}/artifacts/run.json",
                                Message: "Missing run metadata file required to rebuild the run index.",
                                SuggestedFix: "Restore the missing run.json (from source control or backups), or delete the incomplete run directory. (Legacy runs may also have run.json at the run root.)"
                            )
                        );
                    }

                    continue;
                }

                RunMetadataDocument run;
                try
                {
                    var json = File.ReadAllText(runJsonPath);
                    run = JsonSerializer.Deserialize<RunMetadataDocument>(json, JsonOptions)
                          ?? throw new InvalidOperationException("Run metadata JSON deserialized to null.");
                }
                catch (Exception ex) when (ex is JsonException or NotSupportedException)
                {
                    issues.Add(
                        new AosIndexRepairIssue(
                            ContractPath: File.Exists(canonicalRunJsonPath)
                                ? $".aos/evidence/runs/{dirName}/artifacts/run.json"
                                : $".aos/evidence/runs/{dirName}/run.json",
                            Message: "Invalid JSON; cannot reconstruct run index entry.",
                            SuggestedFix: "Fix the JSON (or restore a valid copy), then re-run 'aos repair indexes'."
                        )
                    );
                    continue;
                }

                if (run.SchemaVersion != 1)
                {
                    issues.Add(
                        new AosIndexRepairIssue(
                            ContractPath: File.Exists(canonicalRunJsonPath)
                                ? $".aos/evidence/runs/{dirName}/artifacts/run.json"
                                : $".aos/evidence/runs/{dirName}/run.json",
                            Message: $"Unsupported run metadata schemaVersion '{run.SchemaVersion}'.",
                            SuggestedFix: "Upgrade/downgrade the run metadata to schemaVersion 1 (or delete the run directory), then re-run the repair."
                        )
                    );
                    continue;
                }

                if (!string.Equals(run.RunId, dirName, StringComparison.Ordinal))
                {
                    issues.Add(
                        new AosIndexRepairIssue(
                            ContractPath: File.Exists(canonicalRunJsonPath)
                                ? $".aos/evidence/runs/{dirName}/artifacts/run.json"
                                : $".aos/evidence/runs/{dirName}/run.json",
                            Message: $"runId '{run.RunId}' does not match directory name '{dirName}'.",
                            SuggestedFix: "Fix run.json so runId matches the directory name, or rename the directory to match runId."
                        )
                    );
                    continue;
                }

                if (string.IsNullOrWhiteSpace(run.Status) || (run.Status != "started" && run.Status != "finished" && run.Status != "abandoned"))
                {
                    issues.Add(
                        new AosIndexRepairIssue(
                            ContractPath: File.Exists(canonicalRunJsonPath)
                                ? $".aos/evidence/runs/{dirName}/artifacts/run.json"
                                : $".aos/evidence/runs/{dirName}/run.json",
                            Message: $"Unsupported run status '{run.Status}'. Expected 'started', 'finished', or 'abandoned'.",
                            SuggestedFix: "Fix run.json to use a supported status value, then re-run 'aos repair indexes'."
                        )
                    );
                    continue;
                }

                if (!IsValidRoundtripUtcTimestamp(run.StartedAtUtc))
                {
                    issues.Add(
                        new AosIndexRepairIssue(
                            ContractPath: File.Exists(canonicalRunJsonPath)
                                ? $".aos/evidence/runs/{dirName}/artifacts/run.json"
                                : $".aos/evidence/runs/{dirName}/run.json",
                            Message: $"Invalid startedAtUtc '{run.StartedAtUtc}'. Expected an ISO-8601 roundtrip timestamp.",
                            SuggestedFix: "Fix run.json to use a valid 'O' format timestamp, then re-run 'aos repair indexes'."
                        )
                    );
                    continue;
                }

                if (run.FinishedAtUtc is not null && !IsValidRoundtripUtcTimestamp(run.FinishedAtUtc))
                {
                    issues.Add(
                        new AosIndexRepairIssue(
                            ContractPath: File.Exists(canonicalRunJsonPath)
                                ? $".aos/evidence/runs/{dirName}/artifacts/run.json"
                                : $".aos/evidence/runs/{dirName}/run.json",
                            Message: $"Invalid finishedAtUtc '{run.FinishedAtUtc}'. Expected an ISO-8601 roundtrip timestamp.",
                            SuggestedFix: "Fix run.json to use a valid 'O' format timestamp, then re-run 'aos repair indexes'."
                        )
                    );
                    continue;
                }

                items.Add(
                    new RunIndexItemDocument(
                        RunId: run.RunId,
                        Status: run.Status,
                        StartedAtUtc: run.StartedAtUtc,
                        FinishedAtUtc: run.FinishedAtUtc
                    )
                );
            }
        }

        var ordered = items
            .OrderBy(i => i.RunId, StringComparer.Ordinal)
            .ToArray();

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(
            runsIndexPath,
            new RunIndexDocument(SchemaVersion: 1, Items: ordered),
            JsonOptions,
            writeIndented: true
        );

        return ordered.Length;
    }

    private static bool LooksLikeHex32(string value)
    {
        if (value.Length != 32) return false;
        foreach (var ch in value)
        {
            var isHex =
                (ch >= '0' && ch <= '9') ||
                (ch >= 'a' && ch <= 'f') ||
                (ch >= 'A' && ch <= 'F');
            if (!isHex)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidRoundtripUtcTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return DateTimeOffset.TryParse(value, out _);
    }

    private sealed record RunMetadataDocument(
        int SchemaVersion,
        string RunId,
        string Status,
        string StartedAtUtc,
        string? FinishedAtUtc);

    private sealed record RunIndexDocument(
        int SchemaVersion,
        IReadOnlyList<RunIndexItemDocument> Items);

    private sealed record RunIndexItemDocument(
        string RunId,
        string Status,
        string StartedAtUtc,
        string? FinishedAtUtc);
}

internal sealed record AosIndexRepairResult(
    string AosRootPath,
    IReadOnlyDictionary<AosArtifactKind, int> SpecCatalogCounts,
    int RunCount);

internal sealed record AosIndexRepairIssue(
    string ContractPath,
    string Message,
    string SuggestedFix);

internal sealed class AosIndexRepairFailedException : Exception
{
    public AosIndexRepairFailedException(IReadOnlyList<AosIndexRepairIssue> issues)
        : base("Index repair failed.")
    {
        Issues = issues ?? throw new ArgumentNullException(nameof(issues));
    }

    public IReadOnlyList<AosIndexRepairIssue> Issues { get; }
}

