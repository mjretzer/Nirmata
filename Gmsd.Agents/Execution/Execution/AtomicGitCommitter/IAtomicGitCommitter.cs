namespace Gmsd.Agents.Execution.Execution.AtomicGitCommitter;

/// <summary>
/// Executes atomic Git commits scoped to task-defined file patterns.
/// Only stages files in the intersection of changed files and allowed scopes.
/// </summary>
public interface IAtomicGitCommitter
{
    /// <summary>
    /// Computes the intersection of changed files and allowed scopes, stages those files,
    /// creates a commit with a TSK-based message, and returns commit metadata.
    /// </summary>
    /// <param name="request">The commit request containing task ID, file scopes, changed files, and summary.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The commit result with success status, commit hash, diff statistics, and staged files.</returns>
    Task<CommitResult> CommitAsync(CommitRequest request, CancellationToken cancellationToken = default);
}
