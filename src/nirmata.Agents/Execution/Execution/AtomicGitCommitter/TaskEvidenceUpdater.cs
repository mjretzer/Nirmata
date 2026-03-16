using nirmata.Aos.Engine.Evidence;
using nirmata.Aos.Engine.Evidence.TaskEvidence;

namespace nirmata.Agents.Execution.Execution.AtomicGitCommitter;

/// <summary>
/// Updates task-evidence pointers with commit metadata.
/// </summary>
internal static class TaskEvidenceUpdater
{
    /// <summary>
    /// Updates the task evidence pointer with commit metadata after a successful commit.
    /// </summary>
    /// <param name="aosRootPath">The AOS root path.</param>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="runId">The run identifier.</param>
    /// <param name="commitHash">The Git commit hash, or null if no commit was made.</param>
    /// <param name="diffStat">The computed diff statistics.</param>
    public static void UpdateWithCommit(
        string aosRootPath,
        string taskId,
        string runId,
        string? commitHash,
        DiffStat diffStat)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (string.IsNullOrWhiteSpace(taskId)) throw new ArgumentNullException(nameof(taskId));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));
        if (diffStat is null) throw new ArgumentNullException(nameof(diffStat));

        var diffstat = new AosTaskEvidenceLatestWriter.TaskEvidenceDiffstat(
            FilesChanged: diffStat.FilesChanged,
            Insertions: diffStat.Insertions,
            Deletions: diffStat.Deletions
        );

        AosTaskEvidenceLatestWriter.WriteLatest(
            aosRootPath: aosRootPath,
            taskId: taskId,
            runId: runId,
            gitCommit: commitHash,
            diffstat: diffstat
        );
    }

    /// <summary>
    /// Updates the task evidence pointer when no commit was made (empty intersection or failure).
    /// </summary>
    /// <param name="aosRootPath">The AOS root path.</param>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="runId">The run identifier.</param>
    /// <param name="diffStat">The computed diff statistics.</param>
    public static void UpdateWithoutCommit(
        string aosRootPath,
        string taskId,
        string runId,
        DiffStat diffStat)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (string.IsNullOrWhiteSpace(taskId)) throw new ArgumentNullException(nameof(taskId));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));
        if (diffStat is null) throw new ArgumentNullException(nameof(diffStat));

        var diffstat = new AosTaskEvidenceLatestWriter.TaskEvidenceDiffstat(
            FilesChanged: diffStat.FilesChanged,
            Insertions: diffStat.Insertions,
            Deletions: diffStat.Deletions
        );

        AosTaskEvidenceLatestWriter.WriteLatest(
            aosRootPath: aosRootPath,
            taskId: taskId,
            runId: runId,
            gitCommit: null,
            diffstat: diffstat
        );
    }
}
