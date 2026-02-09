using Gmsd.Agents.Models.Results;

namespace Gmsd.Agents.Execution.Planning.RoadmapModifier;

/// <summary>
/// Represents the status of a roadmap modification operation.
/// </summary>
public enum RoadmapModifyStatus
{
    /// <summary>
    /// The modification completed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// The modification was blocked due to safety checks.
    /// </summary>
    Blocked,

    /// <summary>
    /// The modification failed due to an error.
    /// </summary>
    Failed
}

/// <summary>
/// Encapsulates the result of a roadmap modification operation.
/// </summary>
public sealed class RoadmapModifyResult
{
    /// <summary>
    /// The status of the modification operation.
    /// </summary>
    public RoadmapModifyStatus Status { get; set; }

    /// <summary>
    /// Whether the modification was successful.
    /// </summary>
    public bool IsSuccess => Status == RoadmapModifyStatus.Success;

    /// <summary>
    /// Whether the operation was blocked (e.g., attempting to remove active phase without force).
    /// </summary>
    public bool IsBlocked => Status == RoadmapModifyStatus.Blocked;

    /// <summary>
    /// The operation type that was performed.
    /// </summary>
    public RoadmapModifyOperation Operation { get; set; }

    /// <summary>
    /// The ID of the phase that was inserted or removed (if applicable).
    /// </summary>
    public string? AffectedPhaseId { get; set; }

    /// <summary>
    /// For insert operations: the newly created phase specification.
    /// </summary>
    public PhaseSpec? NewPhase { get; set; }

    /// <summary>
    /// The updated roadmap specification after modification.
    /// </summary>
    public List<PhaseSpec> UpdatedPhases { get; set; } = new();

    /// <summary>
    /// The updated milestone specifications after modification.
    /// </summary>
    public List<MilestoneSpec> UpdatedMilestones { get; set; } = new();

    /// <summary>
    /// Mapping of old phase IDs to new phase IDs when renumbering occurred.
    /// </summary>
    public Dictionary<string, string>? PhaseIdMapping { get; set; }

    /// <summary>
    /// When blocked: the ID of the created issue documenting the blocker.
    /// </summary>
    public string? BlockerIssueId { get; set; }

    /// <summary>
    /// When blocked: the reason for the block.
    /// </summary>
    public string? BlockerReason { get; set; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Error code for programmatic handling of failures.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// The path to the updated roadmap spec file.
    /// </summary>
    public string? RoadmapSpecPath { get; set; }

    /// <summary>
    /// The path to the updated state file.
    /// </summary>
    public string? StatePath { get; set; }

    /// <summary>
    /// Timestamp when the modification started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Timestamp when the modification completed.
    /// </summary>
    public DateTimeOffset CompletedAt { get; set; }

    /// <summary>
    /// Duration of the modification operation.
    /// </summary>
    public TimeSpan Duration => CompletedAt - StartedAt;

    /// <summary>
    /// List of validation errors if the modified roadmap is invalid.
    /// </summary>
    public List<string> ValidationErrors { get; set; } = new();

    /// <summary>
    /// Creates a success result.
    /// </summary>
    public static RoadmapModifyResult SuccessResult(RoadmapModifyOperation operation, string affectedPhaseId)
    {
        return new RoadmapModifyResult
        {
            Status = RoadmapModifyStatus.Success,
            Operation = operation,
            AffectedPhaseId = affectedPhaseId,
            StartedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Creates a blocked result.
    /// </summary>
    public static RoadmapModifyResult BlockedResult(string phaseId, string reason, string? issueId = null)
    {
        return new RoadmapModifyResult
        {
            Status = RoadmapModifyStatus.Blocked,
            Operation = RoadmapModifyOperation.Remove,
            AffectedPhaseId = phaseId,
            BlockerReason = reason,
            BlockerIssueId = issueId,
            StartedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static RoadmapModifyResult FailedResult(string errorMessage, string? errorCode = null)
    {
        return new RoadmapModifyResult
        {
            Status = RoadmapModifyStatus.Failed,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode,
            StartedAt = DateTimeOffset.UtcNow
        };
    }
}
