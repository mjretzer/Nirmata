namespace Gmsd.Agents.Execution.Verification.UatVerifier;

/// <summary>
/// Represents a request to perform UAT verification.
/// </summary>
public sealed record UatVerificationRequest
{
    /// <summary>
    /// The task identifier being verified.
    /// </summary>
    public required string TaskId { get; init; }

    /// <summary>
    /// The run identifier for the execution being verified.
    /// </summary>
    public required string RunId { get; init; }

    /// <summary>
    /// The acceptance criteria to evaluate against execution evidence.
    /// </summary>
    public required IReadOnlyList<AcceptanceCriterion> AcceptanceCriteria { get; init; }

    /// <summary>
    /// The file scopes defining what files are relevant for verification.
    /// </summary>
    public required IReadOnlyList<FileScope> FileScopes { get; init; }
}

/// <summary>
/// Defines an acceptance criterion for UAT verification.
/// </summary>
public sealed record AcceptanceCriterion
{
    /// <summary>
    /// Unique identifier for this criterion.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable description of what is being verified.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The type of check to perform (file-exists, content-contains, build-succeeds, test-passes).
    /// </summary>
    public required string CheckType { get; init; }

    /// <summary>
    /// The target file path or pattern for file-based checks.
    /// </summary>
    public string? TargetPath { get; init; }

    /// <summary>
    /// The expected content pattern for content-contains checks.
    /// </summary>
    public string? ExpectedContent { get; init; }

    /// <summary>
    /// Whether this criterion is required (failure fails verification) or optional.
    /// </summary>
    public bool IsRequired { get; init; } = true;
}

/// <summary>
/// Defines a file scope for verification.
/// </summary>
public sealed record FileScope
{
    /// <summary>
    /// The relative path to the file or directory.
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// The type of scope (read, write, create, modify, delete).
    /// </summary>
    public required string ScopeType { get; init; }

    /// <summary>
    /// Description of what should be verified for this file.
    /// </summary>
    public string? Description { get; init; }
}
