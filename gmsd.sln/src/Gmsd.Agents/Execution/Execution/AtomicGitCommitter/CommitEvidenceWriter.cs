using System.Text.Json;
using Gmsd.Aos.Engine;
using Gmsd.Aos.Engine.Evidence;
using Gmsd.Aos.Engine.Paths;

namespace Gmsd.Agents.Execution.Execution.AtomicGitCommitter;

/// <summary>
/// Writes commit metadata to per-run artifacts using deterministic JSON serialization.
/// </summary>
internal static class CommitEvidenceWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Writes git-diffstat.json artifact before commit with computed diff statistics.
    /// </summary>
    /// <param name="aosRootPath">The AOS root path.</param>
    /// <param name="runId">The run identifier.</param>
    /// <param name="diffStat">The computed diff statistics.</param>
    public static void WriteDiffStat(string aosRootPath, string runId, DiffStat diffStat)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));
        if (diffStat is null) throw new ArgumentNullException(nameof(diffStat));

        var artifactsPath = AosPathRouter.GetRunArtifactsRootPath(aosRootPath, runId);
        var diffStatPath = Path.Combine(artifactsPath, "git-diffstat.json");

        Directory.CreateDirectory(artifactsPath);

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(
            diffStatPath,
            new GitDiffStatDocument(
                SchemaVersion: 1,
                FilesChanged: diffStat.FilesChanged,
                Insertions: diffStat.Insertions,
                Deletions: diffStat.Deletions
            ),
            JsonOptions,
            writeIndented: true
        );
    }

    /// <summary>
    /// Writes git-commit.json artifact after commit with commit metadata.
    /// </summary>
    /// <param name="aosRootPath">The AOS root path.</param>
    /// <param name="runId">The run identifier.</param>
    /// <param name="commitHash">The Git commit hash.</param>
    /// <param name="timestampUtc">The commit timestamp in UTC.</param>
    /// <param name="message">The commit message.</param>
    public static void WriteCommit(
        string aosRootPath,
        string runId,
        string commitHash,
        DateTimeOffset timestampUtc,
        string message)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));
        if (string.IsNullOrWhiteSpace(commitHash)) throw new ArgumentException("Missing commit hash.", nameof(commitHash));
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Missing commit message.", nameof(message));

        var artifactsPath = AosPathRouter.GetRunArtifactsRootPath(aosRootPath, runId);
        var commitPath = Path.Combine(artifactsPath, "git-commit.json");

        Directory.CreateDirectory(artifactsPath);

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(
            commitPath,
            new GitCommitDocument(
                SchemaVersion: 1,
                CommitHash: commitHash.Trim(),
                TimestampUtc: timestampUtc.ToString("O"),
                Message: message.Trim()
            ),
            JsonOptions,
            writeIndented: true
        );
    }

    /// <summary>
    /// Writes git-commit.json artifact when no commit was made (empty intersection or failure).
    /// </summary>
    /// <param name="aosRootPath">The AOS root path.</param>
    /// <param name="runId">The run identifier.</param>
    /// <param name="reason">The reason why no commit was made.</param>
    public static void WriteNoCommit(string aosRootPath, string runId, string reason)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Missing reason.", nameof(reason));

        var artifactsPath = AosPathRouter.GetRunArtifactsRootPath(aosRootPath, runId);
        var commitPath = Path.Combine(artifactsPath, "git-commit.json");

        Directory.CreateDirectory(artifactsPath);

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(
            commitPath,
            new GitCommitDocument(
                SchemaVersion: 1,
                CommitHash: null,
                TimestampUtc: null,
                Message: reason.Trim()
            ),
            JsonOptions,
            writeIndented: true
        );
    }

    private sealed record GitDiffStatDocument(
        int SchemaVersion,
        int FilesChanged,
        int Insertions,
        int Deletions);

    private sealed record GitCommitDocument(
        int SchemaVersion,
        string? CommitHash,
        string? TimestampUtc,
        string Message);
}
