namespace Gmsd.Agents.Execution.Verification.UatVerifier;

/// <summary>
/// Represents the result of a UAT verification run.
/// </summary>
public sealed record UatVerificationResult
{
    /// <summary>
    /// Whether all required checks passed.
    /// </summary>
    public required bool IsPassed { get; init; }

    /// <summary>
    /// The run identifier that was verified.
    /// </summary>
    public required string RunId { get; init; }

    /// <summary>
    /// Individual check results.
    /// </summary>
    public required IReadOnlyList<UatCheckResult> Checks { get; init; }

    /// <summary>
    /// Issues created for failed checks.
    /// </summary>
    public required IReadOnlyList<UatIssueReference> IssuesCreated { get; init; }
}

/// <summary>
/// Represents the result of an individual UAT check.
/// </summary>
public sealed record UatCheckResult
{
    /// <summary>
    /// The criterion ID that was checked.
    /// </summary>
    public required string CriterionId { get; init; }

    /// <summary>
    /// Whether the check passed.
    /// </summary>
    public required bool Passed { get; init; }

    /// <summary>
    /// Human-readable message describing the result.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The type of check performed.
    /// </summary>
    public required string CheckType { get; init; }

    /// <summary>
    /// The target path that was checked (if applicable).
    /// </summary>
    public string? TargetPath { get; init; }

    /// <summary>
    /// The expected value or pattern.
    /// </summary>
    public string? Expected { get; init; }

    /// <summary>
    /// The actual value or observation.
    /// </summary>
    public string? Actual { get; init; }

    /// <summary>
    /// Whether this check was required.
    /// </summary>
    public bool IsRequired { get; init; }
}

/// <summary>
/// Reference to an issue created for a failed check.
/// </summary>
public sealed record UatIssueReference
{
    /// <summary>
    /// The issue identifier.
    /// </summary>
    public required string IssueId { get; init; }

    /// <summary>
    /// The criterion ID that triggered this issue.
    /// </summary>
    public required string CriterionId { get; init; }

    /// <summary>
    /// The file path where the issue was created.
    /// </summary>
    public required string IssuePath { get; init; }
}
