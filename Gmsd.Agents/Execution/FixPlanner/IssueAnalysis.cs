namespace Gmsd.Agents.Execution.FixPlanner;

/// <summary>
/// Represents the analysis of a single issue and recommended fixes.
/// </summary>
public sealed record IssueAnalysis
{
    /// <summary>
    /// The issue ID being analyzed (e.g., "ISS-001").
    /// </summary>
    public required string IssueId { get; init; }

    /// <summary>
    /// The identified root cause of the issue.
    /// </summary>
    public required string RootCause { get; init; }

    /// <summary>
    /// The list of files affected by this issue.
    /// </summary>
    public IReadOnlyList<string> AffectedFiles { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The list of recommended fixes for this issue.
    /// </summary>
    public IReadOnlyList<RecommendedFix> RecommendedFixes { get; init; } = Array.Empty<RecommendedFix>();
}

/// <summary>
/// Represents a single recommended fix for an issue.
/// </summary>
public sealed record RecommendedFix
{
    /// <summary>
    /// Description of what needs to be fixed.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The file path that needs to be modified.
    /// </summary>
    public string? TargetFile { get; init; }

    /// <summary>
    /// The type of fix required (create, modify, delete, refactor).
    /// </summary>
    public required string FixType { get; init; }

    /// <summary>
    /// Estimated complexity of the fix (low, medium, high).
    /// </summary>
    public string? Complexity { get; init; }
}
