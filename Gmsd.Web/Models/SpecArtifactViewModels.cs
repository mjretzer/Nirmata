namespace Gmsd.Web.Models;

public enum SpecItemStatus
{
    Draft,
    Planned,
    InProgress,
    Blocked,
    Completed,
    Verified,
    Failed
}

public enum IssueSeverity
{
    Critical,
    High,
    Medium,
    Low
}

public enum IssueType
{
    Bug,
    Task,
    Enhancement,
    Blocker
}

public enum IssueStatus
{
    Open,
    InProgress,
    Resolved,
    Deferred,
    Closed
}

public class MilestoneViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? TargetDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public SpecItemStatus Status { get; set; }
    public List<PhaseSummaryViewModel> Phases { get; set; } = new();
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int ProgressPercent => TotalTasks > 0 ? (CompletedTasks * 100) / TotalTasks : 0;
    public bool HasCompletionGate { get; set; }
    public string? GateValidationMessage { get; set; }
}

public class PhaseSummaryViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public SpecItemStatus Status { get; set; }
    public int TaskCount { get; set; }
    public int CompletedTaskCount { get; set; }
}

public class PhaseViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string MilestoneId { get; set; } = string.Empty;
    public string MilestoneName { get; set; } = string.Empty;
    public SpecItemStatus Status { get; set; }
    
    public List<string> Goals { get; set; } = new();
    public List<string> Outcomes { get; set; } = new();
    public List<string> Assumptions { get; set; } = new();
    public List<ResearchItemViewModel> Research { get; set; } = new();
    
    public List<TaskSummaryViewModel> Tasks { get; set; } = new();
    public List<ConstraintViewModel> Constraints { get; set; } = new();
    
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedEnd { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }
}

public class ResearchItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string? Findings { get; set; }
    public bool IsComplete { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class TaskSummaryViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public SpecItemStatus Status { get; set; }
    public int? Priority { get; set; }
    public string? Assignee { get; set; }
}

public class TaskViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PhaseId { get; set; } = string.Empty;
    public string PhaseName { get; set; } = string.Empty;
    public string MilestoneId { get; set; } = string.Empty;
    public string MilestoneName { get; set; } = string.Empty;
    public SpecItemStatus Status { get; set; }
    public int? Priority { get; set; }
    public string? Assignee { get; set; }
    
    public List<string> AcceptanceCriteria { get; set; } = new();
    public List<string> ImplementationSteps { get; set; } = new();
    public List<string> VerificationSteps { get; set; } = new();
    
    public string? PlanJson { get; set; }
    public string? UatJson { get; set; }
    public List<LinkViewModel> Links { get; set; } = new();
    
    public string? LatestRunId { get; set; }
    public DateTime? LatestRunAt { get; set; }
    public string? LatestRunStatus { get; set; }
    
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class LinkViewModel
{
    public string Type { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string? TargetName { get; set; }
    public string Relationship { get; set; } = string.Empty;
}

public class IssueViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IssueType Type { get; set; }
    public IssueSeverity Severity { get; set; }
    public IssueStatus Status { get; set; }
    
    public string? ReproSteps { get; set; }
    public string? ExpectedBehavior { get; set; }
    public string? ActualBehavior { get; set; }
    
    public string? TaskId { get; set; }
    public string? TaskName { get; set; }
    public string? PhaseId { get; set; }
    public string? PhaseName { get; set; }
    public string? MilestoneId { get; set; }
    public string? MilestoneName { get; set; }
    
    public string? UatRunId { get; set; }
    public string? UatSessionId { get; set; }
    public string? UatCheckId { get; set; }
    
    public string? AssignedTo { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNotes { get; set; }
}

public class ConstraintViewModel
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsBlocking { get; set; }
    public string? Source { get; set; }
}

public class UatCheckViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool? Passed { get; set; }
    public string? ReproNotes { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? VerifiedBy { get; set; }
    public string? IssueId { get; set; }
}

public class UatSessionViewModel
{
    public string Id { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public List<UatCheckViewModel> Checks { get; set; } = new();
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public int PendingCount { get; set; }
    public List<string> RelatedIssueIds { get; set; } = new();
    public string? LatestRunId { get; set; }
}

public class RoadmapViewModel
{
    public List<MilestoneTimelineViewModel> Milestones { get; set; } = new();
    public string? CurrentPhaseId { get; set; }
    public string? CurrentMilestoneId { get; set; }
    public List<AlignmentWarningViewModel> Warnings { get; set; } = new();
}

public class MilestoneTimelineViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime? TargetDate { get; set; }
    public SpecItemStatus Status { get; set; }
    public List<PhaseTimelineViewModel> Phases { get; set; } = new();
    public int ProgressPercent { get; set; }
}

public class PhaseTimelineViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public SpecItemStatus Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int DurationDays { get; set; }
}

public class AlignmentWarningViewModel
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? EntityType { get; set; }
    public string? SuggestedAction { get; set; }
}
