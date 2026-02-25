using System.Text;
using System.Text.Json;
using Gmsd.Aos.Engine;
using Gmsd.Aos.Engine.Errors;
using Gmsd.Aos.Engine.Evidence.Runs;
using Gmsd.Aos.Engine.ExecutePlan;
using Gmsd.Aos.Engine.Paths;
using System.Security.Cryptography;

namespace Gmsd.Aos.Engine.Evidence;

internal static class AosRunEvidenceScaffolder
{
    private static readonly Utf8EncodingNoBom Utf8NoBom = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void EnsureRunEvidenceScaffold(
        string aosRootPath,
        string runId,
        DateTimeOffset startedAtUtc,
        string command,
        IReadOnlyList<string> args)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));
        if (string.IsNullOrWhiteSpace(command)) throw new ArgumentException("Missing command.", nameof(command));
        if (args is null) throw new ArgumentNullException(nameof(args));

        var runRootPath = AosPathRouter.GetRunEvidenceRootPath(aosRootPath, runId);
        var logsPath = AosPathRouter.GetRunLogsRootPath(aosRootPath, runId);
        var outputsPath = AosPathRouter.GetRunOutputsRootPath(aosRootPath, runId);
        var artifactsPath = AosPathRouter.GetRunArtifactsRootPath(aosRootPath, runId);
        var runJsonPath = AosPathRouter.GetRunMetadataPath(aosRootPath, runId);
        var packetJsonPath = AosPathRouter.GetRunPacketPath(aosRootPath, runId);
        var runsIndexJsonPath = AosPathRouter.GetRunsIndexPath(aosRootPath);

        Directory.CreateDirectory(runRootPath);
        Directory.CreateDirectory(logsPath);
        Directory.CreateDirectory(outputsPath);
        Directory.CreateDirectory(artifactsPath);

        // Canonical deterministic JSON (stable bytes + atomic write semantics).
        DeterministicJsonFileWriter.WriteCanonicalJsonIfMissing(
            runJsonPath,
            new RunMetadataDocument(
                SchemaVersion: 1,
                RunId: runId,
                Status: "started",
                StartedAtUtc: startedAtUtc.ToString("O"),
                FinishedAtUtc: null
            ),
            JsonOptions
        );

        DeterministicJsonFileWriter.WriteCanonicalJsonIfMissing(
            packetJsonPath,
            new RunPacketDocument(
                SchemaVersion: 1,
                RunId: runId,
                Command: command,
                Args: args.ToArray(),
                ExecutePlan: null
            ),
            JsonOptions
        );

        // Ensure per-run view artifacts exist for discoverability (they can be updated later).
        AosRunCommandsViewWriter.EnsureRunCommandsViewExists(aosRootPath, runId);
        AosRunSummaryWriter.EnsureRunSummaryExistsAtStart(aosRootPath, runId, startedAtUtc: startedAtUtc.ToString("O"));

        EnsureRunIndexIncludesRun(
            runsIndexJsonPath,
            new RunIndexItemDocument(
                RunId: runId,
                Status: "started",
                StartedAtUtc: startedAtUtc.ToString("O"),
                FinishedAtUtc: null
            )
        );
    }

    public static void PopulateExecutePlanPacketFields(
        string aosRootPath,
        string runId,
        IReadOnlyList<string> args,
        string planPath,
        ExecutePlanPlan plan)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));
        if (args is null) throw new ArgumentNullException(nameof(args));
        if (string.IsNullOrWhiteSpace(planPath)) throw new ArgumentNullException(nameof(planPath));
        if (plan is null) throw new ArgumentNullException(nameof(plan));

        var packetJsonPath = AosPathRouter.GetRunPacketPath(aosRootPath, runId);

        var plannedOutputs = plan.Outputs
            .Select(o => (o.RelativePath ?? string.Empty).Replace('\\', '/'))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        var canonicalPlanSha256 = TryComputeCanonicalJsonSha256LowerHex(planPath);

        var policyPath = Path.Combine(aosRootPath, "config", "policy.json");
        var canonicalPolicySha256 = File.Exists(policyPath)
            ? TryComputeCanonicalJsonSha256LowerHex(policyPath)
            : null;

        var configPath = Path.Combine(aosRootPath, "config", "config.json");
        var canonicalConfigSha256 = File.Exists(configPath)
            ? TryComputeCanonicalJsonSha256LowerHex(configPath)
            : null;

        var planPathRel = TryGetRelativePathWithinRepository(aosRootPath, planPath);

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(
            packetJsonPath,
            new RunPacketDocument(
                SchemaVersion: 1,
                RunId: runId,
                Command: "execute-plan",
                Args: args.ToArray(),
                ExecutePlan: new RunPacketExecutePlanDocument(
                    PlanPath: planPathRel,
                    PlanSha256: canonicalPlanSha256,
                    PlannedOutputs: plannedOutputs,
                    PolicyContractPath: Gmsd.Aos.Engine.Policy.AosPolicyLoader.PolicyContractPath,
                    PolicySha256: canonicalPolicySha256,
                    ConfigContractPath: Gmsd.Aos.Engine.Config.AosConfigLoader.ConfigContractPath,
                    ConfigSha256: canonicalConfigSha256
                )
            ),
            JsonOptions
        );
    }

    public static void FinishRun(string aosRootPath, string runId, DateTimeOffset finishedAtUtc)
        => FinishRun(aosRootPath, runId, finishedAtUtc, additionalProducedArtifacts: null);

    public static void FinishRun(
        string aosRootPath,
        string runId,
        DateTimeOffset finishedAtUtc,
        IReadOnlyList<(string Kind, string ContractPath, string? Sha256)>? additionalProducedArtifacts)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));

        var canonicalRunJsonPath = AosPathRouter.GetRunMetadataPath(aosRootPath, runId);
        var legacyRunJsonPath = AosPathRouter.GetLegacyRunMetadataPath(aosRootPath, runId);
        var runJsonPath = File.Exists(canonicalRunJsonPath) ? canonicalRunJsonPath : legacyRunJsonPath;
        var runsIndexJsonPath = AosPathRouter.GetRunsIndexPath(aosRootPath);
        var resultJsonPath = AosPathRouter.GetRunResultPath(aosRootPath, runId);

        if (!File.Exists(runJsonPath))
        {
            throw new FileNotFoundException(
                $"Run metadata not found at '{canonicalRunJsonPath}' (canonical) or '{legacyRunJsonPath}' (legacy).",
                canonicalRunJsonPath
            );
        }

        RunMetadataDocument run;
        try
        {
            var json = File.ReadAllText(runJsonPath, Utf8NoBom.Instance);
            run = JsonSerializer.Deserialize<RunMetadataDocument>(json, JsonOptions)
                  ?? throw new InvalidOperationException("Run metadata JSON deserialized to null.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new InvalidOperationException($"Invalid run metadata JSON at '{runJsonPath}'.", ex);
        }

        if (run.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Unsupported run metadata schemaVersion '{run.SchemaVersion}' at '{runJsonPath}'.");
        }

        // Transition to finished deterministically (only the timestamp changes).
        var updatedRun = run with
        {
            Status = "finished",
            FinishedAtUtc = finishedAtUtc.ToString("O")
        };

        Directory.CreateDirectory(Path.GetDirectoryName(canonicalRunJsonPath)!);
        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(runJsonPath, updatedRun, JsonOptions);
        if (!string.Equals(runJsonPath, canonicalRunJsonPath, StringComparison.Ordinal))
        {
            // Best-effort: also materialize the canonical location for transition/migration friendliness.
            DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(canonicalRunJsonPath, updatedRun, JsonOptions);
        }

        UpdateRunIndexForFinishedRun(
            runsIndexJsonPath,
            runId,
            finishedAtUtc.ToString("O")
        );

        // Produce the run manifest (outputs + hashes) on finish.
        AosRunManifestWriter.WriteRunManifest(aosRootPath, runId);

        // Produce the run result artifact on finish.
        var producedArtifacts = CollectProducedArtifactsFromManifestIfPresent(
            aosRootPath,
            runId,
            additionalProducedArtifacts
        );
        WriteRunResult(
            resultJsonPath,
            new RunResultDocument(
                SchemaVersion: 1,
                RunId: runId,
                Status: "succeeded",
                ExitCode: 0,
                Error: null,
                ProducedArtifacts: producedArtifacts
            )
        );

        // Produce the run summary last (it links to canonical artifacts).
        AosRunSummaryWriter.WriteRunSummary(
            aosRootPath,
            runId,
            status: "succeeded",
            startedAtUtc: updatedRun.StartedAtUtc,
            finishedAtUtc: updatedRun.FinishedAtUtc,
            exitCode: 0
        );
    }

    public static void FailRun(
        string aosRootPath,
        string runId,
        DateTimeOffset finishedAtUtc,
        int exitCode,
        AosErrorEnvelope error,
        IReadOnlyList<(string Kind, string ContractPath, string? Sha256)>? additionalProducedArtifacts = null)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));
        if (error is null) throw new ArgumentNullException(nameof(error));

        var canonicalRunJsonPath = AosPathRouter.GetRunMetadataPath(aosRootPath, runId);
        var legacyRunJsonPath = AosPathRouter.GetLegacyRunMetadataPath(aosRootPath, runId);
        var runJsonPath = File.Exists(canonicalRunJsonPath) ? canonicalRunJsonPath : legacyRunJsonPath;
        var runsIndexJsonPath = AosPathRouter.GetRunsIndexPath(aosRootPath);
        var resultJsonPath = AosPathRouter.GetRunResultPath(aosRootPath, runId);

        if (!File.Exists(runJsonPath))
        {
            throw new FileNotFoundException(
                $"Run metadata not found at '{canonicalRunJsonPath}' (canonical) or '{legacyRunJsonPath}' (legacy).",
                canonicalRunJsonPath
            );
        }

        RunMetadataDocument run;
        try
        {
            var json = File.ReadAllText(runJsonPath, Utf8NoBom.Instance);
            run = JsonSerializer.Deserialize<RunMetadataDocument>(json, JsonOptions)
                  ?? throw new InvalidOperationException("Run metadata JSON deserialized to null.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new InvalidOperationException($"Invalid run metadata JSON at '{runJsonPath}'.", ex);
        }

        if (run.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Unsupported run metadata schemaVersion '{run.SchemaVersion}' at '{runJsonPath}'.");
        }

        var updatedRun = run with
        {
            Status = "finished",
            FinishedAtUtc = finishedAtUtc.ToString("O")
        };

        Directory.CreateDirectory(Path.GetDirectoryName(canonicalRunJsonPath)!);
        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(runJsonPath, updatedRun, JsonOptions);
        if (!string.Equals(runJsonPath, canonicalRunJsonPath, StringComparison.Ordinal))
        {
            // Best-effort: also materialize the canonical location for transition/migration friendliness.
            DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(canonicalRunJsonPath, updatedRun, JsonOptions);
        }

        UpdateRunIndexForFinishedRun(
            runsIndexJsonPath,
            runId,
            finishedAtUtc.ToString("O")
        );

        // Best-effort: produce a manifest (if outputs exist) so result can reference it.
        try
        {
            AosRunManifestWriter.WriteRunManifest(aosRootPath, runId);
        }
        catch
        {
            // Best-effort only.
        }

        var producedArtifacts = CollectProducedArtifactsFromManifestIfPresent(
            aosRootPath,
            runId,
            additionalProducedArtifacts
        );
        WriteRunResult(
            resultJsonPath,
            new RunResultDocument(
                SchemaVersion: 1,
                RunId: runId,
                Status: "failed",
                ExitCode: exitCode,
                Error: error,
                ProducedArtifacts: producedArtifacts
            )
        );

        // Produce the run summary last (it links to canonical artifacts).
        AosRunSummaryWriter.WriteRunSummary(
            aosRootPath,
            runId,
            status: "failed",
            startedAtUtc: updatedRun.StartedAtUtc,
            finishedAtUtc: updatedRun.FinishedAtUtc,
            exitCode: exitCode
        );
    }

    public static bool TryWriteFailedRunResultIfRunExists(
        string aosRootPath,
        string runId,
        int exitCode,
        AosErrorEnvelope error)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));
        if (error is null) throw new ArgumentNullException(nameof(error));

        // Avoid creating "ghost" run evidence: only write a result if the run metadata exists.
        var canonicalRunJsonPath = AosPathRouter.GetRunMetadataPath(aosRootPath, runId);
        var legacyRunJsonPath = AosPathRouter.GetLegacyRunMetadataPath(aosRootPath, runId);
        if (!File.Exists(canonicalRunJsonPath) && !File.Exists(legacyRunJsonPath))
        {
            return false;
        }

        try
        {
            var resultJsonPath = AosPathRouter.GetRunResultPath(aosRootPath, runId);
            var producedArtifacts = CollectProducedArtifactsFromManifestIfPresent(
                aosRootPath,
                runId,
                additionalProducedArtifacts: null
            );
            WriteRunResult(
                resultJsonPath,
                new RunResultDocument(
                    SchemaVersion: 1,
                    RunId: runId,
                    Status: "failed",
                    ExitCode: exitCode,
                    Error: error,
                    ProducedArtifacts: producedArtifacts
                )
            );
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void MarkRunAbandoned(string aosRootPath, string runId, DateTimeOffset abandonedAtUtc)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));

        var canonicalRunJsonPath = AosPathRouter.GetRunMetadataPath(aosRootPath, runId);
        var legacyRunJsonPath = AosPathRouter.GetLegacyRunMetadataPath(aosRootPath, runId);
        var runJsonPath = File.Exists(canonicalRunJsonPath) ? canonicalRunJsonPath : legacyRunJsonPath;
        var runsIndexJsonPath = AosPathRouter.GetRunsIndexPath(aosRootPath);

        if (!File.Exists(runJsonPath))
        {
            throw new FileNotFoundException(
                $"Run metadata not found at '{canonicalRunJsonPath}' (canonical) or '{legacyRunJsonPath}' (legacy).",
                canonicalRunJsonPath
            );
        }

        RunMetadataDocument run;
        try
        {
            var json = File.ReadAllText(runJsonPath, Utf8NoBom.Instance);
            run = JsonSerializer.Deserialize<RunMetadataDocument>(json, JsonOptions)
                  ?? throw new InvalidOperationException("Run metadata JSON deserialized to null.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new InvalidOperationException($"Invalid run metadata JSON at '{runJsonPath}'.", ex);
        }

        if (run.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Unsupported run metadata schemaVersion '{run.SchemaVersion}' at '{runJsonPath}'.");
        }

        // Only mark as abandoned if not already finished
        if (string.Equals(run.Status, "finished", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Transition to abandoned deterministically
        var updatedRun = run with
        {
            Status = "abandoned",
            FinishedAtUtc = abandonedAtUtc.ToString("O")
        };

        Directory.CreateDirectory(Path.GetDirectoryName(canonicalRunJsonPath)!);
        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(runJsonPath, updatedRun, JsonOptions);
        if (!string.Equals(runJsonPath, canonicalRunJsonPath, StringComparison.Ordinal))
        {
            DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(canonicalRunJsonPath, updatedRun, JsonOptions);
        }

        UpdateRunIndexForAbandonedRun(
            runsIndexJsonPath,
            runId,
            abandonedAtUtc.ToString("O")
        );
    }

    public static void PauseRun(string aosRootPath, string runId, DateTimeOffset pausedAtUtc)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));

        var canonicalRunJsonPath = AosPathRouter.GetRunMetadataPath(aosRootPath, runId);
        var legacyRunJsonPath = AosPathRouter.GetLegacyRunMetadataPath(aosRootPath, runId);
        var runJsonPath = File.Exists(canonicalRunJsonPath) ? canonicalRunJsonPath : legacyRunJsonPath;
        var runsIndexJsonPath = AosPathRouter.GetRunsIndexPath(aosRootPath);

        if (!File.Exists(runJsonPath))
        {
            throw new FileNotFoundException(
                $"Run metadata not found at '{canonicalRunJsonPath}' (canonical) or '{legacyRunJsonPath}' (legacy).",
                canonicalRunJsonPath
            );
        }

        RunMetadataDocument run;
        try
        {
            var json = File.ReadAllText(runJsonPath, Utf8NoBom.Instance);
            run = JsonSerializer.Deserialize<RunMetadataDocument>(json, JsonOptions)
                  ?? throw new InvalidOperationException("Run metadata JSON deserialized to null.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new InvalidOperationException($"Invalid run metadata JSON at '{runJsonPath}'.", ex);
        }

        if (run.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Unsupported run metadata schemaVersion '{run.SchemaVersion}' at '{runJsonPath}'.");
        }

        // Only pause if currently in "started" status
        if (!string.Equals(run.Status, "started", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Cannot pause run in '{run.Status}' status. Only 'started' runs can be paused.");
        }

        // Transition to paused
        var updatedRun = run with
        {
            Status = "paused"
        };

        Directory.CreateDirectory(Path.GetDirectoryName(canonicalRunJsonPath)!);
        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(runJsonPath, updatedRun, JsonOptions);
        if (!string.Equals(runJsonPath, canonicalRunJsonPath, StringComparison.Ordinal))
        {
            DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(canonicalRunJsonPath, updatedRun, JsonOptions);
        }

        UpdateRunIndexForPausedRun(runsIndexJsonPath, runId);
    }

    public static void ResumeRun(string aosRootPath, string runId, DateTimeOffset resumedAtUtc)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));

        var canonicalRunJsonPath = AosPathRouter.GetRunMetadataPath(aosRootPath, runId);
        var legacyRunJsonPath = AosPathRouter.GetLegacyRunMetadataPath(aosRootPath, runId);
        var runJsonPath = File.Exists(canonicalRunJsonPath) ? canonicalRunJsonPath : legacyRunJsonPath;
        var runsIndexJsonPath = AosPathRouter.GetRunsIndexPath(aosRootPath);

        if (!File.Exists(runJsonPath))
        {
            throw new FileNotFoundException(
                $"Run metadata not found at '{canonicalRunJsonPath}' (canonical) or '{legacyRunJsonPath}' (legacy).",
                canonicalRunJsonPath
            );
        }

        RunMetadataDocument run;
        try
        {
            var json = File.ReadAllText(runJsonPath, Utf8NoBom.Instance);
            run = JsonSerializer.Deserialize<RunMetadataDocument>(json, JsonOptions)
                  ?? throw new InvalidOperationException("Run metadata JSON deserialized to null.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new InvalidOperationException($"Invalid run metadata JSON at '{runJsonPath}'.", ex);
        }

        if (run.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Unsupported run metadata schemaVersion '{run.SchemaVersion}' at '{runJsonPath}'.");
        }

        // Only resume if currently in "paused" status
        if (!string.Equals(run.Status, "paused", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Cannot resume run in '{run.Status}' status. Only 'paused' runs can be resumed.");
        }

        // Transition back to started
        var updatedRun = run with
        {
            Status = "started"
        };

        Directory.CreateDirectory(Path.GetDirectoryName(canonicalRunJsonPath)!);
        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(runJsonPath, updatedRun, JsonOptions);
        if (!string.Equals(runJsonPath, canonicalRunJsonPath, StringComparison.Ordinal))
        {
            DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(canonicalRunJsonPath, updatedRun, JsonOptions);
        }

        UpdateRunIndexForResumedRun(runsIndexJsonPath, runId);
    }

    private static void WriteRunResult(string resultJsonPath, RunResultDocument doc)
    {
        if (resultJsonPath is null) throw new ArgumentNullException(nameof(resultJsonPath));
        if (doc is null) throw new ArgumentNullException(nameof(doc));

        Directory.CreateDirectory(Path.GetDirectoryName(resultJsonPath)!);
        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(resultJsonPath, doc, JsonOptions);
    }

    private static string? TryComputeCanonicalJsonSha256LowerHex(string path)
    {
        try
        {
            var json = File.ReadAllText(path, Utf8NoBom.Instance);
            using var doc = JsonDocument.Parse(json);
            var canonical = DeterministicJsonFileWriter.CanonicalizeToUtf8Bytes(doc.RootElement, writeIndented: true);
            var hash = SHA256.HashData(canonical);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetRelativePathWithinRepository(string aosRootPath, string path)
    {
        try
        {
            var repoRoot = Directory.GetParent(aosRootPath)?.FullName;
            if (string.IsNullOrWhiteSpace(repoRoot))
            {
                return null;
            }

            var full = Path.GetFullPath(path, repoRoot);
            var rel = Path.GetRelativePath(repoRoot, full);
            if (string.IsNullOrWhiteSpace(rel))
            {
                return null;
            }

            // If the relative path escapes the repo root, treat as non-deterministic external reference.
            if (rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel))
            {
                return null;
            }

            return rel.Replace('\\', '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }
        catch
        {
            return null;
        }
    }

    private static RunProducedArtifactDocument[] CollectProducedArtifactsFromManifestIfPresent(
        string aosRootPath,
        string runId,
        IReadOnlyList<(string Kind, string ContractPath, string? Sha256)>? additionalProducedArtifacts)
    {
        var manifestPath = AosPathRouter.GetRunManifestPath(aosRootPath, runId);
        var legacyManifestPath = AosPathRouter.GetLegacyRunManifestPath(aosRootPath, runId);
        var effectiveManifestPath = File.Exists(manifestPath) ? manifestPath : legacyManifestPath;

        var artifacts = new List<RunProducedArtifactDocument>();

        // Always reference the manifest if present (it is the canonical output inventory).
        if (File.Exists(effectiveManifestPath))
        {
            artifacts.Add(
                new RunProducedArtifactDocument(
                    Kind: "manifest",
                    ContractPath: $".aos/evidence/runs/{runId}/artifacts/manifest.json",
                    Sha256: null
                )
            );

            try
            {
                var json = File.ReadAllText(effectiveManifestPath, Utf8NoBom.Instance);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("outputs", out var outputs)
                    && outputs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var o in outputs.EnumerateArray())
                    {
                        if (o.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        if (!o.TryGetProperty("relativePath", out var relEl) || relEl.ValueKind != JsonValueKind.String)
                        {
                            continue;
                        }

                        var rel = (relEl.GetString() ?? string.Empty).Trim();
                        if (string.IsNullOrWhiteSpace(rel))
                        {
                            continue;
                        }

                        // Contract path MUST use forward slashes.
                        rel = rel.Replace('\\', '/');

                        var sha = o.TryGetProperty("sha256", out var shaEl) && shaEl.ValueKind == JsonValueKind.String
                            ? shaEl.GetString()
                            : null;

                        artifacts.Add(
                            new RunProducedArtifactDocument(
                                Kind: "output",
                                ContractPath: $".aos/evidence/runs/{runId}/outputs/{rel}",
                                Sha256: string.IsNullOrWhiteSpace(sha) ? null : sha
                            )
                        );
                    }
                }
            }
            catch
            {
                // Best-effort only; the manifest is already written and is itself referenced.
            }
        }

        if (additionalProducedArtifacts is not null)
        {
            foreach (var a in additionalProducedArtifacts)
            {
                if (string.IsNullOrWhiteSpace(a.ContractPath) || string.IsNullOrWhiteSpace(a.Kind))
                {
                    continue;
                }

                // Contract path MUST use forward slashes.
                var contractPath = a.ContractPath.Replace('\\', '/');
                artifacts.Add(
                    new RunProducedArtifactDocument(
                        Kind: a.Kind,
                        ContractPath: contractPath,
                        Sha256: string.IsNullOrWhiteSpace(a.Sha256) ? null : a.Sha256
                    )
                );
            }
        }

        // Arrays are not canonicalized by the JSON writer; keep ordering deterministic.
        return artifacts
            .OrderBy(a => a.ContractPath, StringComparer.Ordinal)
            .ToArray();
    }

    private static void EnsureRunIndexIncludesRun(string runsIndexJsonPath, RunIndexItemDocument run)
    {
        if (runsIndexJsonPath is null) throw new ArgumentNullException(nameof(runsIndexJsonPath));
        if (run is null) throw new ArgumentNullException(nameof(run));
        if (!AosRunId.IsValid(run.RunId)) throw new ArgumentException("Invalid run id.", nameof(run));

        RunIndexDocument doc;

        if (!File.Exists(runsIndexJsonPath))
        {
            doc = new RunIndexDocument(SchemaVersion: 1, Items: Array.Empty<RunIndexItemDocument>());
        }
        else
        {
            try
            {
                var json = File.ReadAllText(runsIndexJsonPath, Utf8NoBom.Instance);
                doc = JsonSerializer.Deserialize<RunIndexDocument>(json, JsonOptions)
                      ?? throw new InvalidOperationException("Run index JSON deserialized to null.");
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                throw new InvalidOperationException($"Invalid run index JSON at '{runsIndexJsonPath}'.", ex);
            }
        }

        if (doc.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Unsupported run index schemaVersion '{doc.SchemaVersion}' at '{runsIndexJsonPath}'.");
        }

        var items = doc.Items.ToList();

        // Idempotent: do not modify an existing entry on "start".
        var alreadyExists = items.Any(i => string.Equals(i.RunId, run.RunId, StringComparison.Ordinal));
        if (!alreadyExists)
        {
            items.Add(run);
        }

        var ordered = items
            .OrderBy(i => i.RunId, StringComparer.Ordinal)
            .ToArray();

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(
            runsIndexJsonPath,
            new RunIndexDocument(SchemaVersion: 1, Items: ordered),
            JsonOptions
        );
    }

    private static void UpdateRunIndexForFinishedRun(string runsIndexJsonPath, string runId, string finishedAtUtc)
    {
        if (runsIndexJsonPath is null) throw new ArgumentNullException(nameof(runsIndexJsonPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));
        if (string.IsNullOrWhiteSpace(finishedAtUtc)) throw new ArgumentException("Missing finishedAtUtc.", nameof(finishedAtUtc));

        if (!File.Exists(runsIndexJsonPath))
        {
            throw new FileNotFoundException($"Run index not found at '{runsIndexJsonPath}'.", runsIndexJsonPath);
        }

        RunIndexDocument doc;
        try
        {
            var json = File.ReadAllText(runsIndexJsonPath, Utf8NoBom.Instance);
            doc = JsonSerializer.Deserialize<RunIndexDocument>(json, JsonOptions)
                  ?? throw new InvalidOperationException("Run index JSON deserialized to null.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new InvalidOperationException($"Invalid run index JSON at '{runsIndexJsonPath}'.", ex);
        }

        if (doc.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Unsupported run index schemaVersion '{doc.SchemaVersion}' at '{runsIndexJsonPath}'.");
        }

        var items = doc.Items.ToList();
        var idx = items.FindIndex(i => string.Equals(i.RunId, runId, StringComparison.Ordinal));
        if (idx < 0)
        {
            throw new InvalidOperationException($"Run '{runId}' not found in run index '{runsIndexJsonPath}'.");
        }

        var existing = items[idx];
        items[idx] = existing with { Status = "finished", FinishedAtUtc = finishedAtUtc };

        var ordered = items
            .OrderBy(i => i.RunId, StringComparer.Ordinal)
            .ToArray();

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(
            runsIndexJsonPath,
            new RunIndexDocument(SchemaVersion: 1, Items: ordered),
            JsonOptions
        );
    }

    private static void UpdateRunIndexForAbandonedRun(string runsIndexJsonPath, string runId, string abandonedAtUtc)
    {
        if (runsIndexJsonPath is null) throw new ArgumentNullException(nameof(runsIndexJsonPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));
        if (string.IsNullOrWhiteSpace(abandonedAtUtc)) throw new ArgumentException("Missing abandonedAtUtc.", nameof(abandonedAtUtc));

        if (!File.Exists(runsIndexJsonPath))
        {
            throw new FileNotFoundException($"Run index not found at '{runsIndexJsonPath}'.", runsIndexJsonPath);
        }

        RunIndexDocument doc;
        try
        {
            var json = File.ReadAllText(runsIndexJsonPath, Utf8NoBom.Instance);
            doc = JsonSerializer.Deserialize<RunIndexDocument>(json, JsonOptions)
                  ?? throw new InvalidOperationException("Run index JSON deserialized to null.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new InvalidOperationException($"Invalid run index JSON at '{runsIndexJsonPath}'.", ex);
        }

        if (doc.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Unsupported run index schemaVersion '{doc.SchemaVersion}' at '{runsIndexJsonPath}'.");
        }

        var items = doc.Items.ToList();
        var idx = items.FindIndex(i => string.Equals(i.RunId, runId, StringComparison.Ordinal));
        if (idx < 0)
        {
            throw new InvalidOperationException($"Run '{runId}' not found in run index '{runsIndexJsonPath}'.");
        }

        var existing = items[idx];
        items[idx] = existing with { Status = "abandoned", FinishedAtUtc = abandonedAtUtc };

        var ordered = items
            .OrderBy(i => i.RunId, StringComparer.Ordinal)
            .ToArray();

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(
            runsIndexJsonPath,
            new RunIndexDocument(SchemaVersion: 1, Items: ordered),
            JsonOptions
        );
    }

    private static void UpdateRunIndexForPausedRun(string runsIndexJsonPath, string runId)
    {
        if (runsIndexJsonPath is null) throw new ArgumentNullException(nameof(runsIndexJsonPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));

        if (!File.Exists(runsIndexJsonPath))
        {
            throw new FileNotFoundException($"Run index not found at '{runsIndexJsonPath}'.", runsIndexJsonPath);
        }

        RunIndexDocument doc;
        try
        {
            var json = File.ReadAllText(runsIndexJsonPath, Utf8NoBom.Instance);
            doc = JsonSerializer.Deserialize<RunIndexDocument>(json, JsonOptions)
                  ?? throw new InvalidOperationException("Run index JSON deserialized to null.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new InvalidOperationException($"Invalid run index JSON at '{runsIndexJsonPath}'.", ex);
        }

        if (doc.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Unsupported run index schemaVersion '{doc.SchemaVersion}' at '{runsIndexJsonPath}'.");
        }

        var items = doc.Items.ToList();
        var idx = items.FindIndex(i => string.Equals(i.RunId, runId, StringComparison.Ordinal));
        if (idx < 0)
        {
            throw new InvalidOperationException($"Run '{runId}' not found in run index '{runsIndexJsonPath}'.");
        }

        var existing = items[idx];
        items[idx] = existing with { Status = "paused" };

        var ordered = items
            .OrderBy(i => i.RunId, StringComparer.Ordinal)
            .ToArray();

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(
            runsIndexJsonPath,
            new RunIndexDocument(SchemaVersion: 1, Items: ordered),
            JsonOptions
        );
    }

    private static void UpdateRunIndexForResumedRun(string runsIndexJsonPath, string runId)
    {
        if (runsIndexJsonPath is null) throw new ArgumentNullException(nameof(runsIndexJsonPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));

        if (!File.Exists(runsIndexJsonPath))
        {
            throw new FileNotFoundException($"Run index not found at '{runsIndexJsonPath}'.", runsIndexJsonPath);
        }

        RunIndexDocument doc;
        try
        {
            var json = File.ReadAllText(runsIndexJsonPath, Utf8NoBom.Instance);
            doc = JsonSerializer.Deserialize<RunIndexDocument>(json, JsonOptions)
                  ?? throw new InvalidOperationException("Run index JSON deserialized to null.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new InvalidOperationException($"Invalid run index JSON at '{runsIndexJsonPath}'.", ex);
        }

        if (doc.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Unsupported run index schemaVersion '{doc.SchemaVersion}' at '{runsIndexJsonPath}'.");
        }

        var items = doc.Items.ToList();
        var idx = items.FindIndex(i => string.Equals(i.RunId, runId, StringComparison.Ordinal));
        if (idx < 0)
        {
            throw new InvalidOperationException($"Run '{runId}' not found in run index '{runsIndexJsonPath}'.");
        }

        var existing = items[idx];
        items[idx] = existing with { Status = "started" };

        var ordered = items
            .OrderBy(i => i.RunId, StringComparer.Ordinal)
            .ToArray();

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(
            runsIndexJsonPath,
            new RunIndexDocument(SchemaVersion: 1, Items: ordered),
            JsonOptions
        );
    }

    private sealed class Utf8EncodingNoBom
    {
        public Encoding Instance { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }

    private sealed record RunMetadataDocument(
        int SchemaVersion,
        string RunId,
        string Status,
        string StartedAtUtc,
        string? FinishedAtUtc);

    private sealed record RunPacketDocument(
        int SchemaVersion,
        string RunId,
        string Command,
        IReadOnlyList<string> Args,
        RunPacketExecutePlanDocument? ExecutePlan);

    private sealed record RunPacketExecutePlanDocument(
        string? PlanPath,
        string? PlanSha256,
        IReadOnlyList<string> PlannedOutputs,
        string PolicyContractPath,
        string? PolicySha256,
        string ConfigContractPath,
        string? ConfigSha256);

    private sealed record RunResultDocument(
        int SchemaVersion,
        string RunId,
        string Status,
        int ExitCode,
        AosErrorEnvelope? Error,
        IReadOnlyList<RunProducedArtifactDocument> ProducedArtifacts);

    private sealed record RunProducedArtifactDocument(
        string Kind,
        string ContractPath,
        string? Sha256);

    private sealed record RunIndexDocument(
        int SchemaVersion,
        IReadOnlyList<RunIndexItemDocument> Items);

    private sealed record RunIndexItemDocument(
        string RunId,
        string Status,
        string StartedAtUtc,
        string? FinishedAtUtc);
}

