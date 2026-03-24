namespace nirmata.Data.Dto.Models.Spec;

/// <summary>
/// A single UAT record read from <c>.aos/spec/uat/UAT-####.json</c>
/// or <c>.aos/spec/tasks/TSK-*/uat.json</c>.
/// </summary>
public sealed class UatRecordDto
{
    public required string Id { get; init; }
    public string? TaskId { get; init; }
    public string? PhaseId { get; init; }
    public required string Status { get; init; }
    public string? Observations { get; init; }
    public string? ReproSteps { get; init; }
    public IReadOnlyList<UatCheckDto> Checks { get; init; } = [];
}

/// <summary>
/// A single acceptance-criterion result within a <see cref="UatRecordDto"/>.
/// </summary>
public sealed class UatCheckDto
{
    public required string CriterionId { get; init; }
    public required bool Passed { get; init; }
    public string? Message { get; init; }
    public string? CheckType { get; init; }
    public string? TargetPath { get; init; }
    public string? Expected { get; init; }
    public string? Actual { get; init; }
}

/// <summary>
/// Derived pass/fail state for a single task, aggregated from all UAT records that reference it.
/// </summary>
public sealed class UatTaskSummaryDto
{
    public required string TaskId { get; init; }

    /// <summary>One of: <c>passed</c>, <c>failed</c>, <c>unknown</c>.</summary>
    public required string Status { get; init; }
    public required int RecordCount { get; init; }
}

/// <summary>
/// Derived pass/fail state for a single phase, aggregated from its task summaries.
/// </summary>
public sealed class UatPhaseSummaryDto
{
    public required string PhaseId { get; init; }

    /// <summary>One of: <c>passed</c>, <c>failed</c>, <c>unknown</c>.</summary>
    public required string Status { get; init; }
    public IReadOnlyList<string> TaskIds { get; init; } = [];
}

/// <summary>
/// Full UAT summary response returned by <c>GET /v1/workspaces/{wsId}/uat</c>.
/// </summary>
public sealed class UatSummaryDto
{
    /// <summary>All UAT records found in the workspace spec store.</summary>
    public IReadOnlyList<UatRecordDto> Records { get; init; } = [];

    /// <summary>Pass/fail derivation per task.</summary>
    public IReadOnlyList<UatTaskSummaryDto> TaskSummaries { get; init; } = [];

    /// <summary>Pass/fail derivation per phase.</summary>
    public IReadOnlyList<UatPhaseSummaryDto> PhaseSummaries { get; init; } = [];
}
