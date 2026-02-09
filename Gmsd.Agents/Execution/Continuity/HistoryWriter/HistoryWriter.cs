using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gmsd.Aos.Engine.Evidence;
using Gmsd.Aos.Engine.Paths;

namespace Gmsd.Agents.Execution.Continuity.HistoryWriter;

/// <summary>
/// Appends durable narrative history entries to .aos/spec/summary.md.
/// Links runs and tasks to their evidence artifacts and commit hashes.
/// </summary>
internal sealed class HistoryWriter : IHistoryWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _aosRootPath;
    private readonly string _summaryPath;
    private readonly string _lockFilePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryWriter"/> class.
    /// </summary>
    /// <param name="aosRootPath">The absolute path to the AOS root directory.</param>
    public HistoryWriter(string aosRootPath)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (!Directory.Exists(aosRootPath))
        {
            throw new ArgumentException($"AOS root path does not exist: {aosRootPath}", nameof(aosRootPath));
        }

        _aosRootPath = aosRootPath;
        _summaryPath = Path.Combine(aosRootPath, "spec", "summary.md");
        _lockFilePath = Path.Combine(aosRootPath, "cache", "history-writer.lock");
    }

    /// <inheritdoc />
    public string SummaryPath => _summaryPath;

    /// <inheritdoc />
    public async Task<HistoryEntry> AppendAsync(string runId, string? taskId = null, string? narrative = null, CancellationToken ct = default)
    {
        if (runId is null) throw new ArgumentNullException(nameof(runId));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));

        // Read evidence for the run
        var evidence = await ReadRunEvidenceAsync(runId, taskId, ct);
        if (evidence is null)
        {
            throw new FileNotFoundException($"Evidence not found for run '{runId}'.");
        }

        // Generate the history entry key (RUN-xxx or RUN-xxx/TSK-xxx)
        var key = taskId is not null ? $"{runId}/{taskId}" : runId;

        // Get commit hash from git if available
        var commitHash = await GetGitCommitHashAsync(ct);

        // Build evidence pointers from the run evidence
        var evidencePointers = BuildEvidencePointers(runId, evidence);

        // Create verification proof from evidence
        var verificationProof = BuildVerificationProof(evidence);

        // Build the history entry
        var entry = new HistoryEntry
        {
            SchemaVersion = "1.0",
            Key = key,
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            RunId = runId,
            TaskId = taskId,
            Verification = verificationProof,
            CommitHash = commitHash,
            Evidence = evidencePointers,
            Narrative = narrative
        };

        // Append entry to summary.md with safe concurrent access
        await AppendEntryToSummaryAsync(entry, ct);

        return entry;
    }

    /// <inheritdoc />
    public bool Exists(string runId, string? taskId = null)
    {
        if (runId is null) throw new ArgumentNullException(nameof(runId));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));

        var key = taskId is not null ? $"{runId}/{taskId}" : runId;

        if (!File.Exists(_summaryPath))
        {
            return false;
        }

        try
        {
            var content = File.ReadAllText(_summaryPath);
            return content.Contains($"### {key}");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Reads the evidence summary for a run.
    /// </summary>
    private async Task<RunEvidenceSummary?> ReadRunEvidenceAsync(string runId, string? taskId, CancellationToken ct)
    {
        var summaryPath = AosPathRouter.GetRunSummaryPath(_aosRootPath, runId);

        if (!File.Exists(summaryPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(summaryPath, ct);
            var doc = JsonDocument.Parse(json);

            var runIdValue = doc.RootElement.GetProperty("runId").GetString() ?? runId;
            var status = doc.RootElement.GetProperty("status").GetString() ?? "unknown";
            var startedAtUtc = doc.RootElement.GetProperty("startedAtUtc").GetString() ?? DateTimeOffset.UtcNow.ToString("O");

            string? finishedAtUtc = null;
            if (doc.RootElement.TryGetProperty("finishedAtUtc", out var finishedProp) && finishedProp.ValueKind != JsonValueKind.Null)
            {
                finishedAtUtc = finishedProp.GetString();
            }

            int exitCode = 0;
            if (doc.RootElement.TryGetProperty("exitCode", out var exitCodeProp))
            {
                exitCode = exitCodeProp.GetInt32();
            }

            // Read artifacts paths
            var artifactsPaths = new List<string>();
            if (doc.RootElement.TryGetProperty("artifacts", out var artifactsProp))
            {
                if (artifactsProp.TryGetProperty("runMetadata", out var runMetadataProp))
                {
                    var runMetadata = runMetadataProp.GetString();
                    if (!string.IsNullOrEmpty(runMetadata))
                    {
                        artifactsPaths.Add(runMetadata);
                    }
                }
                if (artifactsProp.TryGetProperty("packet", out var packetProp))
                {
                    var packet = packetProp.GetString();
                    if (!string.IsNullOrEmpty(packet))
                    {
                        artifactsPaths.Add(packet);
                    }
                }
                if (artifactsProp.TryGetProperty("result", out var resultProp))
                {
                    var result = resultProp.GetString();
                    if (!string.IsNullOrEmpty(result))
                    {
                        artifactsPaths.Add(result);
                    }
                }
                if (artifactsProp.TryGetProperty("commands", out var commandsProp))
                {
                    var commands = commandsProp.GetString();
                    if (!string.IsNullOrEmpty(commands))
                    {
                        artifactsPaths.Add(commands);
                    }
                }
            }

            return new RunEvidenceSummary
            {
                RunId = runIdValue,
                Status = status,
                StartedAtUtc = startedAtUtc,
                FinishedAtUtc = finishedAtUtc,
                ExitCode = exitCode,
                ArtifactsPaths = artifactsPaths
            };
        }
        catch (Exception ex) when (ex is JsonException || ex is KeyNotFoundException)
        {
            // Evidence file exists but is malformed - return minimal evidence
            return new RunEvidenceSummary
            {
                RunId = runId,
                Status = "unknown",
                StartedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                ExitCode = 0,
                ArtifactsPaths = new List<string>()
            };
        }
    }

    /// <summary>
    /// Builds evidence pointers from run evidence.
    /// </summary>
    private IReadOnlyList<EvidencePointer> BuildEvidencePointers(string runId, RunEvidenceSummary evidence)
    {
        var pointers = new List<EvidencePointer>
        {
            new EvidencePointer
            {
                Type = "summary",
                Path = $".aos/evidence/runs/{runId}/summary.json",
                Description = "Run summary with status and metadata"
            }
        };

        // Add artifact pointers
        foreach (var artifactPath in evidence.ArtifactsPaths)
        {
            var type = DetermineArtifactType(artifactPath);
            pointers.Add(new EvidencePointer
            {
                Type = type,
                Path = artifactPath,
                Description = $"{type} artifact for run {runId}"
            });
        }

        return pointers;
    }

    /// <summary>
    /// Determines the artifact type from its path.
    /// </summary>
    private static string DetermineArtifactType(string path)
    {
        var fileName = Path.GetFileName(path).ToLowerInvariant();
        return fileName switch
        {
            "run.json" => "metadata",
            "packet.json" => "packet",
            "result.json" => "result",
            "manifest.json" => "manifest",
            "commands.json" => "commands",
            _ => "artifact"
        };
    }

    /// <summary>
    /// Builds verification proof from evidence.
    /// </summary>
    private VerificationProof BuildVerificationProof(RunEvidenceSummary evidence)
    {
        var status = evidence.ExitCode == 0 ? "passed" : "failed";
        var method = evidence.ExitCode == 0 ? "run-verifier" : "run-failure-detector";

        return new VerificationProof
        {
            Status = status,
            Method = method,
            Issues = evidence.ExitCode != 0 ? 1 : null,
            Details = evidence.ExitCode != 0 ? $"Run exited with code {evidence.ExitCode}" : "Run completed successfully"
        };
    }

    /// <summary>
    /// Attempts to get the current git commit hash.
    /// Returns null if not in a git repository or git is not available.
    /// </summary>
    private static async Task<string?> GetGitCommitHashAsync(CancellationToken ct)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse HEAD",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode == 0)
            {
                return output.Trim();
            }
        }
        catch
        {
            // Git not available or other error - commit hash is optional
        }

        return null;
    }

    /// <summary>
    /// Appends a history entry to the summary.md file with safe concurrent access.
    /// Uses a simple file lock mechanism to prevent concurrent writes.
    /// </summary>
    private async Task AppendEntryToSummaryAsync(HistoryEntry entry, CancellationToken ct)
    {
        // Ensure the spec directory exists
        var specDir = Path.GetDirectoryName(_summaryPath);
        if (!string.IsNullOrEmpty(specDir) && !Directory.Exists(specDir))
        {
            Directory.CreateDirectory(specDir);
        }

        // Ensure the cache directory exists for the lock file
        var cacheDir = Path.GetDirectoryName(_lockFilePath);
        if (!string.IsNullOrEmpty(cacheDir) && !Directory.Exists(cacheDir))
        {
            Directory.CreateDirectory(cacheDir);
        }

        // Acquire lock using simple file-based locking
        await AcquireLockAsync(ct);

        try
        {
            // Build the markdown entry
            var markdownEntry = BuildMarkdownEntry(entry);

            // Append to file (creates if doesn't exist)
            await File.AppendAllTextAsync(_summaryPath, markdownEntry, Encoding.UTF8, ct);
        }
        finally
        {
            // Release lock
            ReleaseLock();
        }
    }

    /// <summary>
    /// Builds a markdown representation of a history entry.
    /// </summary>
    private static string BuildMarkdownEntry(HistoryEntry entry)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"### {entry.Key}");
        sb.AppendLine();
        sb.AppendLine($"**Timestamp:** {entry.Timestamp}");

        if (!string.IsNullOrEmpty(entry.CommitHash))
        {
            sb.AppendLine($"**Commit:** `{entry.CommitHash}`");
        }

        sb.AppendLine($"**Verification:** {entry.Verification.Status} (via {entry.Verification.Method})");

        if (entry.Verification.Issues.HasValue)
        {
            sb.AppendLine($"**Issues:** {entry.Verification.Issues.Value}");
        }

        if (!string.IsNullOrEmpty(entry.Verification.Details))
        {
            sb.AppendLine($"**Details:** {entry.Verification.Details}");
        }

        if (entry.Evidence.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Evidence:**");
            foreach (var pointer in entry.Evidence)
            {
                sb.AppendLine($"- `{pointer.Type}`: {pointer.Path}");
            }
        }

        if (!string.IsNullOrEmpty(entry.Narrative))
        {
            sb.AppendLine();
            sb.AppendLine("**Narrative:**");
            sb.AppendLine(entry.Narrative);
        }

        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Acquires a lock for safe concurrent access using a lock file.
    /// </summary>
    private async Task AcquireLockAsync(CancellationToken ct)
    {
        var maxRetries = 50;
        var retryDelay = TimeSpan.FromMilliseconds(100);

        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                // Try to create the lock file exclusively
                var lockFileStream = new FileStream(
                    _lockFilePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);

                // Write the process ID to identify the lock owner
                using (var writer = new StreamWriter(lockFileStream))
                {
                    await writer.WriteAsync(Environment.ProcessId.ToString());
                }

                return; // Lock acquired
            }
            catch (IOException)
            {
                // Lock file exists, check if it's stale
                try
                {
                    var lockContent = await File.ReadAllTextAsync(_lockFilePath, ct);
                    if (int.TryParse(lockContent, out var pid))
                    {
                        // Check if the process still exists
                        try
                        {
                            var process = System.Diagnostics.Process.GetProcessById(pid);
                            // Process exists, wait and retry
                        }
                        catch (ArgumentException)
                        {
                            // Process doesn't exist, lock is stale
                            try
                            {
                                File.Delete(_lockFilePath);
                            }
                            catch
                            {
                                // Ignore delete errors
                            }
                        }
                    }
                }
                catch
                {
                    // Can't read lock file, assume it's stale
                }

                // Wait before retrying
                await Task.Delay(retryDelay, ct);
            }
        }

        throw new TimeoutException("Failed to acquire history writer lock after maximum retries.");
    }

    /// <summary>
    /// Releases the lock file.
    /// </summary>
    private void ReleaseLock()
    {
        try
        {
            if (File.Exists(_lockFilePath))
            {
                File.Delete(_lockFilePath);
            }
        }
        catch
        {
            // Ignore delete errors - lock will timeout eventually
        }
    }

    /// <summary>
    /// Internal representation of run evidence summary.
    /// </summary>
    private sealed class RunEvidenceSummary
    {
        public required string RunId { get; init; }
        public required string Status { get; init; }
        public required string StartedAtUtc { get; init; }
        public string? FinishedAtUtc { get; init; }
        public required int ExitCode { get; init; }
        public required IReadOnlyList<string> ArtifactsPaths { get; init; }
    }
}
