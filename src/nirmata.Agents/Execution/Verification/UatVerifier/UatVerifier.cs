using nirmata.Agents.Execution.Verification.Issues;
using nirmata.Aos.Public;

namespace nirmata.Agents.Execution.Verification.UatVerifier;

/// <summary>
/// Implementation of the UAT verifier that orchestrates verification checks.
/// </summary>
public sealed class UatVerifier : IUatVerifier
{
    private readonly IUatCheckRunner _checkRunner;
    private readonly IIssueWriter _issueWriter;
    private readonly IUatResultWriter _resultWriter;
    private readonly IWorkspace _workspace;

    /// <summary>
    /// Initializes a new instance of the <see cref="UatVerifier"/> class.
    /// </summary>
    public UatVerifier(
        IUatCheckRunner checkRunner,
        IIssueWriter issueWriter,
        IUatResultWriter resultWriter,
        IWorkspace workspace)
    {
        _checkRunner = checkRunner ?? throw new ArgumentNullException(nameof(checkRunner));
        _issueWriter = issueWriter ?? throw new ArgumentNullException(nameof(issueWriter));
        _resultWriter = resultWriter ?? throw new ArgumentNullException(nameof(resultWriter));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    /// <inheritdoc />
    public async Task<UatVerificationResult> VerifyAsync(UatVerificationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var workspacePath = _workspace.RepositoryRootPath;
        var checkResults = new List<UatCheckResult>();
        var issuesCreated = new List<UatIssueReference>();

        // Run all acceptance criteria checks
        foreach (var criterion in request.AcceptanceCriteria)
        {
            var result = await _checkRunner.RunCheckAsync(criterion, workspacePath, ct);
            checkResults.Add(result);

            // Create issues for failed required checks
            if (!result.Passed && criterion.IsRequired)
            {
                var issue = await _issueWriter.CreateIssueAsync(
                    request.TaskId,
                    request.RunId,
                    result,
                    ct);

                issuesCreated.Add(new UatIssueReference
                {
                    IssueId = issue.Id,
                    CriterionId = criterion.Id,
                    IssuePath = issue.FilePath
                });
            }
        }

        // Determine overall pass/fail
        var allRequiredPassed = checkResults
            .Where(r => r.IsRequired)
            .All(r => r.Passed);

        var verificationResult = new UatVerificationResult
        {
            IsPassed = allRequiredPassed,
            RunId = request.RunId,
            Checks = checkResults.AsReadOnly(),
            IssuesCreated = issuesCreated.AsReadOnly()
        };

        // Write UAT result artifact
        await _resultWriter.WriteResultAsync(
            request.TaskId,
            request.RunId,
            verificationResult,
            ct);

        return verificationResult;
    }
}
