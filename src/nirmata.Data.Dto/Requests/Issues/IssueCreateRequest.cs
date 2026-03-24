using System.ComponentModel.DataAnnotations;

namespace nirmata.Data.Dto.Requests.Issues;

/// <summary>
/// Request body for creating a new workspace issue.
/// </summary>
public sealed class IssueCreateRequest
{
    [Required]
    [MaxLength(500)]
    public required string Title { get; init; }

    /// <summary>Severity level, e.g. <c>low</c>, <c>medium</c>, <c>high</c>, <c>critical</c>.</summary>
    public string? Severity { get; init; }

    /// <summary>Scope description — what area of the system is affected.</summary>
    public string? Scope { get; init; }

    /// <summary>Steps to reproduce the issue.</summary>
    public string? Repro { get; init; }

    /// <summary>What was expected to happen.</summary>
    public string? Expected { get; init; }

    /// <summary>What actually happened.</summary>
    public string? Actual { get; init; }

    /// <summary>File paths affected by this issue.</summary>
    public IReadOnlyList<string>? ImpactedFiles { get; init; }

    public string? PhaseId { get; init; }
    public string? TaskId { get; init; }
    public string? MilestoneId { get; init; }
}
