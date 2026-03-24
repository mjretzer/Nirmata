namespace nirmata.Data.Dto.Models.State;

public sealed class ContinuityStateDto
{
    public StatePositionDto? Position { get; init; }
    public IReadOnlyList<StateDecisionDto> Decisions { get; init; } = [];
    public IReadOnlyList<StateBlockerDto> Blockers { get; init; } = [];
    public StateTransitionDto? LastTransition { get; init; }
}

public sealed class StatePositionDto
{
    public string? MilestoneId { get; init; }
    public string? PhaseId { get; init; }
    public string? TaskId { get; init; }
    public int? StepIndex { get; init; }
    public string? Status { get; init; }
}

public sealed class StateDecisionDto
{
    public string? Id { get; init; }
    public string? Topic { get; init; }
    public string? Decision { get; init; }
    public string? Rationale { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
}

public sealed class StateBlockerDto
{
    public string? Id { get; init; }
    public string? Description { get; init; }
    public string? AffectedTask { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
}

public sealed class StateTransitionDto
{
    public string? From { get; init; }
    public string? To { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
    public string? Trigger { get; init; }
}
