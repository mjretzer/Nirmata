namespace nirmata.Agents.Execution.Verification.UatVerifier;

/// <summary>
/// Defines the contract for running UAT checks.
/// </summary>
public interface IUatCheckRunner
{
    /// <summary>
    /// Runs a single acceptance criterion check.
    /// </summary>
    /// <param name="criterion">The criterion to evaluate.</param>
    /// <param name="workspacePath">The workspace root path.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The check result.</returns>
    Task<UatCheckResult> RunCheckAsync(AcceptanceCriterion criterion, string workspacePath, CancellationToken ct = default);
}

/// <summary>
/// Supported UAT check types.
/// </summary>
public static class UatCheckTypes
{
    public const string FileExists = "file-exists";
    public const string ContentContains = "content-contains";
    public const string BuildSucceeds = "build-succeeds";
    public const string TestPasses = "test-passes";
}
