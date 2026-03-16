using nirmata.Agents.Execution.Verification.UatVerifier;

namespace nirmata.Agents.Execution.Verification.Issues;

/// <summary>
/// Defines the contract for writing issue artifacts.
/// </summary>
public interface IIssueWriter
{
    /// <summary>
    /// Creates an issue for a failed UAT check.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="runId">The run identifier.</param>
    /// <param name="checkResult">The failed check result.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created issue reference.</returns>
    Task<IssueCreated> CreateIssueAsync(string taskId, string runId, UatCheckResult checkResult, CancellationToken ct = default);
}

/// <summary>
/// Reference to a created issue.
/// </summary>
public sealed record IssueCreated
{
    /// <summary>
    /// The issue identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The file path where the issue was created.
    /// </summary>
    public required string FilePath { get; init; }
}
