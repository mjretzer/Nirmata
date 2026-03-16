using nirmata.Agents.Execution.Execution.AtomicGitCommitter;

namespace nirmata.Agents.Tests.Fakes;

/// <summary>
/// Fake implementation of IAtomicGitCommitter for unit testing.
/// </summary>
public sealed class FakeAtomicGitCommitter : IAtomicGitCommitter
{
    private CommitResult? _nextResult;

    /// <summary>
    /// Gets the last commit request received.
    /// </summary>
    public CommitRequest? LastCommitRequest { get; private set; }

    /// <summary>
    /// Gets the last commit result returned.
    /// </summary>
    public CommitResult? LastCommitResult { get; private set; }

    /// <inheritdoc />
    public Task<CommitResult> CommitAsync(CommitRequest request, CancellationToken cancellationToken = default)
    {
        LastCommitRequest = request;

        if (_nextResult is null)
        {
            throw new InvalidOperationException(
                "No result configured for FakeAtomicGitCommitter. " +
                "Call SetupCommitResult before invoking CommitAsync.");
        }

        var result = _nextResult;
        LastCommitResult = result;
        _nextResult = null;

        return Task.FromResult(result);
    }

    /// <summary>
    /// Configures the result to be returned on the next CommitAsync call.
    /// </summary>
    public void SetupCommitResult(CommitResult result)
    {
        _nextResult = result;
    }

    /// <summary>
    /// Resets the fake, clearing all recorded requests and configured results.
    /// </summary>
    public void Reset()
    {
        _nextResult = null;
        LastCommitRequest = null;
        LastCommitResult = null;
    }
}
