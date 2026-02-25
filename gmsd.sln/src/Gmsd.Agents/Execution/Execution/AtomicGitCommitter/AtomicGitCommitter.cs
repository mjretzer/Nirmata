using Gmsd.Aos.Public;
using Microsoft.Extensions.Logging;

namespace Gmsd.Agents.Execution.Execution.AtomicGitCommitter;

/// <summary>
/// Executes atomic Git commits scoped to task-defined file patterns.
/// Only stages files in the intersection of changed files and allowed scopes.
/// </summary>
public sealed class AtomicGitCommitter : IAtomicGitCommitter
{
    private readonly IWorkspace _workspace;
    private readonly ILogger<AtomicGitCommitter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AtomicGitCommitter"/> class.
    /// </summary>
    public AtomicGitCommitter(IWorkspace workspace, ILogger<AtomicGitCommitter> logger)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<CommitResult> CommitAsync(CommitRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // Compute intersection of changed files and allowed scopes
            var filesToStage = StagingIntersection.Compute(request.ChangedFiles, request.FileScopes);

            if (filesToStage.Count == 0)
            {
                _logger.LogInformation(
                    "No files to stage for task {TaskId}. Intersection of changed files and allowed scopes is empty.",
                    request.TaskId);

                return new CommitResult
                {
                    IsSuccess = true,
                    CommitHash = null,
                    DiffStat = new DiffStat { FilesChanged = 0, Insertions = 0, Deletions = 0 },
                    ErrorMessage = null,
                    FilesStaged = Array.Empty<string>()
                };
            }

            // Initialize git command runner
            var gitRunner = new GitCommandRunner(_workspace.RepositoryRootPath);

            // Check if this is a valid git repository
            if (!await gitRunner.IsGitRepositoryAsync(cancellationToken))
            {
                return new CommitResult
                {
                    IsSuccess = false,
                    CommitHash = null,
                    DiffStat = null,
                    ErrorMessage = "Not a valid git repository.",
                    FilesStaged = Array.Empty<string>()
                };
            }

            // Stage the files
            _logger.LogInformation(
                "Staging {FileCount} files for task {TaskId}: {Files}",
                filesToStage.Count,
                request.TaskId,
                string.Join(", ", filesToStage));

            var stageResult = await gitRunner.StageFilesAsync(filesToStage, cancellationToken);
            if (!stageResult.IsSuccess)
            {
                _logger.LogError(
                    "Failed to stage files for task {TaskId}: {Error}",
                    request.TaskId,
                    stageResult.Stderr);

                return new CommitResult
                {
                    IsSuccess = false,
                    CommitHash = null,
                    DiffStat = null,
                    ErrorMessage = $"Failed to stage files: {stageResult.Stderr}",
                    FilesStaged = Array.Empty<string>()
                };
            }

            // Get diff stats before commit
            var diffStat = await ComputeDiffStatAsync(gitRunner, filesToStage, cancellationToken);

            // Create the commit message
            var commitMessage = $"{request.TaskId}: {request.Summary}";

            // Execute commit
            _logger.LogInformation(
                "Creating commit for task {TaskId} with message: {Message}",
                request.TaskId,
                commitMessage);

            var commitResult = await gitRunner.CommitAsync(commitMessage, cancellationToken);
            if (!commitResult.IsSuccess)
            {
                _logger.LogError(
                    "Failed to create commit for task {TaskId}: {Error}",
                    request.TaskId,
                    commitResult.Stderr);

                return new CommitResult
                {
                    IsSuccess = false,
                    CommitHash = null,
                    DiffStat = diffStat,
                    ErrorMessage = $"Failed to create commit: {commitResult.Stderr}",
                    FilesStaged = filesToStage
                };
            }

            // Get the commit hash
            var commitHash = await gitRunner.GetHeadCommitHashAsync(cancellationToken);
            if (string.IsNullOrEmpty(commitHash))
            {
                _logger.LogWarning(
                    "Commit succeeded but could not retrieve commit hash for task {TaskId}",
                    request.TaskId);
            }
            else
            {
                _logger.LogInformation(
                    "Successfully created commit {CommitHash} for task {TaskId}",
                    commitHash,
                    request.TaskId);
            }

            return new CommitResult
            {
                IsSuccess = true,
                CommitHash = commitHash,
                DiffStat = diffStat,
                ErrorMessage = null,
                FilesStaged = filesToStage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error during atomic git commit for task {TaskId}",
                request.TaskId);

            return new CommitResult
            {
                IsSuccess = false,
                CommitHash = null,
                DiffStat = null,
                ErrorMessage = $"Unexpected error: {ex.Message}",
                FilesStaged = Array.Empty<string>()
            };
        }
    }

    private static async Task<DiffStat> ComputeDiffStatAsync(
        GitCommandRunner gitRunner,
        IReadOnlyList<string> stagedFiles,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get diff stats for staged changes
            var statResult = await gitRunner.GetStagedDiffStatAsync(cancellationToken);

            if (string.IsNullOrEmpty(statResult))
            {
                return new DiffStat
                {
                    FilesChanged = stagedFiles.Count,
                    Insertions = 0,
                    Deletions = 0
                };
            }

            // Parse diff stat output (format: "file.txt | 10 +++++-----")
            var lines = statResult.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            int totalInsertions = 0;
            int totalDeletions = 0;

            foreach (var line in lines)
            {
                // Look for the summary line at the end which has the totals
                if (line.Contains("changed") && (line.Contains("insertion") || line.Contains("deletion")))
                {
                    var parts = line.Split(',');
                    foreach (var part in parts)
                    {
                        var trimmed = part.Trim();
                        if (trimmed.Contains("insertion"))
                        {
                            var numStr = string.Concat(trimmed.TakeWhile(char.IsDigit));
                            if (int.TryParse(numStr, out var insertions))
                            {
                                totalInsertions = insertions;
                            }
                        }
                        else if (trimmed.Contains("deletion"))
                        {
                            var numStr = string.Concat(trimmed.TakeWhile(char.IsDigit));
                            if (int.TryParse(numStr, out var deletions))
                            {
                                totalDeletions = deletions;
                            }
                        }
                    }
                }
            }

            return new DiffStat
            {
                FilesChanged = stagedFiles.Count,
                Insertions = totalInsertions,
                Deletions = totalDeletions
            };
        }
        catch
        {
            // Return default stats if parsing fails
            return new DiffStat
            {
                FilesChanged = stagedFiles.Count,
                Insertions = 0,
                Deletions = 0
            };
        }
    }
}
