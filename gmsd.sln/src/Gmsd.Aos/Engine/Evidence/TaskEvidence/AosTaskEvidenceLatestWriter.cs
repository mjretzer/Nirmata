using System.Text.Json;
using Gmsd.Aos.Engine.Paths;

namespace Gmsd.Aos.Engine.Evidence.TaskEvidence;

internal static class AosTaskEvidenceLatestWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void WriteLatest(
        string aosRootPath,
        string taskId,
        string runId,
        string? gitCommit = null,
        TaskEvidenceDiffstat? diffstat = null)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (string.IsNullOrWhiteSpace(taskId)) throw new ArgumentNullException(nameof(taskId));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));

        var path = AosPathRouter.GetTaskEvidenceLatestPath(aosRootPath, taskId);

        var d = diffstat ?? TaskEvidenceDiffstat.Unknown();

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(
            path,
            new TaskEvidenceLatestDocument(
                SchemaVersion: 1,
                TaskId: taskId.Trim(),
                RunId: runId,
                GitCommit: string.IsNullOrWhiteSpace(gitCommit) ? null : gitCommit.Trim(),
                Diffstat: new TaskEvidenceDiffstatDocument(
                    FilesChanged: d.FilesChanged,
                    Insertions: d.Insertions,
                    Deletions: d.Deletions
                )
            ),
            JsonOptions,
            writeIndented: true
        );
    }

    internal static void WriteLatestForTest(
        string aosRootPath,
        string taskId,
        string runId,
        string? gitCommit,
        TaskEvidenceDiffstat? diffstat,
        Action<string> afterTempWriteBeforeCommit)
    {
        if (afterTempWriteBeforeCommit is null) throw new ArgumentNullException(nameof(afterTempWriteBeforeCommit));

        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (string.IsNullOrWhiteSpace(taskId)) throw new ArgumentNullException(nameof(taskId));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));

        var path = AosPathRouter.GetTaskEvidenceLatestPath(aosRootPath, taskId);
        var d = diffstat ?? TaskEvidenceDiffstat.Unknown();

        var bytes = DeterministicJsonFileWriter.SerializeToCanonicalUtf8Bytes(
            new TaskEvidenceLatestDocument(
                SchemaVersion: 1,
                TaskId: taskId.Trim(),
                RunId: runId,
                GitCommit: string.IsNullOrWhiteSpace(gitCommit) ? null : gitCommit.Trim(),
                Diffstat: new TaskEvidenceDiffstatDocument(
                    FilesChanged: d.FilesChanged,
                    Insertions: d.Insertions,
                    Deletions: d.Deletions
                )
            ),
            JsonOptions,
            writeIndented: true
        );

        DeterministicJsonFileWriter.WriteAtomicOverwriteForTest(
            path,
            bytes,
            avoidChurn: true,
            afterTempWriteBeforeCommit: afterTempWriteBeforeCommit
        );
    }

    internal sealed record TaskEvidenceDiffstat(int FilesChanged, int Insertions, int Deletions)
    {
        public static TaskEvidenceDiffstat Unknown() => new(0, 0, 0);
    }

    private sealed record TaskEvidenceLatestDocument(
        int SchemaVersion,
        string TaskId,
        string RunId,
        string? GitCommit,
        TaskEvidenceDiffstatDocument Diffstat);

    private sealed record TaskEvidenceDiffstatDocument(
        int FilesChanged,
        int Insertions,
        int Deletions);
}

