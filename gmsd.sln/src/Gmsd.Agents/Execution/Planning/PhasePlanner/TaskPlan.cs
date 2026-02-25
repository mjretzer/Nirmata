namespace Gmsd.Agents.Execution.Planning.PhasePlanner;

/// <summary>
/// Represents a task plan containing decomposed tasks for a phase.
/// </summary>
public sealed record TaskPlan
{
    /// <summary>
    /// The unique identifier for this task plan.
    /// </summary>
    public required string PlanId { get; init; }

    /// <summary>
    /// The phase identifier this plan is for.
    /// </summary>
    public required string PhaseId { get; init; }

    /// <summary>
    /// The run identifier that generated this plan.
    /// </summary>
    public required string RunId { get; init; }

    /// <summary>
    /// The decomposed tasks for this phase (limited to 2-3 tasks).
    /// </summary>
    public IReadOnlyList<TaskSpecification> Tasks { get; init; } = Array.Empty<TaskSpecification>();

    /// <summary>
    /// Whether the plan was successfully created.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Validation errors if the plan is invalid.
    /// </summary>
    public IReadOnlyList<string> ValidationErrors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// When the plan was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When the plan was completed.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// The path to the plan.json file.
    /// </summary>
    public string PlanJsonPath { get; init; } = string.Empty;

    /// <summary>
    /// Summary of the planning outcome.
    /// </summary>
    public string? Summary { get; init; }
}

/// <summary>
/// Represents a specific task within a phase plan.
/// </summary>
public sealed record TaskSpecification
{
    /// <summary>
    /// The unique identifier for this task (e.g., TSK-0001).
    /// </summary>
    public required string TaskId { get; init; }

    /// <summary>
    /// The phase identifier this task belongs to.
    /// </summary>
    public required string PhaseId { get; init; }

    /// <summary>
    /// Human-readable title for the task.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Detailed description of what the task entails.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The sequence order of this task within the phase.
    /// </summary>
    public int SequenceOrder { get; init; }

    /// <summary>
    /// Files that are in scope for this task.
    /// </summary>
    public IReadOnlyList<FileScope> FileScopes { get; init; } = Array.Empty<FileScope>();

    /// <summary>
    /// Verification steps to confirm task completion.
    /// </summary>
    public IReadOnlyList<VerificationStep> VerificationSteps { get; init; } = Array.Empty<VerificationStep>();

    /// <summary>
    /// Acceptance criteria for the task.
    /// </summary>
    public IReadOnlyList<string> AcceptanceCriteria { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Estimated complexity (low, medium, high).
    /// </summary>
    public string Complexity { get; init; } = "medium";

    /// <summary>
    /// Dependencies on other tasks.
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The path to the task.json file.
    /// </summary>
    public string TaskJsonPath { get; init; } = string.Empty;
}

/// <summary>
/// Defines the scope for a specific file in a task.
/// </summary>
public sealed record FileScope
{
    /// <summary>
    /// The workspace-relative path to the file.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// The type of scope (read, write, create, modify, delete).
    /// </summary>
    public required string ScopeType { get; init; }

    /// <summary>
    /// Description of what will be done with this file.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether the file must exist before the task starts.
    /// </summary>
    public bool MustExist { get; init; }
}

/// <summary>
/// Represents a verification step for task completion.
/// </summary>
public sealed record VerificationStep
{
    /// <summary>
    /// The type of verification (test, compile, lint, manual_review, etc.).
    /// </summary>
    public required string VerificationType { get; init; }

    /// <summary>
    /// Description of what needs to be verified.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Expected outcome of the verification.
    /// </summary>
    public string? ExpectedOutcome { get; init; }

    /// <summary>
    /// Command or procedure to run for verification (if applicable).
    /// </summary>
    public string? Command { get; init; }
}
