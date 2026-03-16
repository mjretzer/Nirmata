namespace nirmata.Agents.Execution.Execution.TaskExecutor;

/// <summary>
/// Represents a task plan loaded from plan.json.
/// </summary>
public sealed class TaskPlanModel
{
    /// <summary>
    /// Schema version of the plan format.
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// The task identifier.
    /// </summary>
    public string TaskId { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable title for the task.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Detailed description of what the task entails.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Files that are in scope for this task.
    /// </summary>
    public IReadOnlyList<FileScopeEntry> FileScopes { get; init; } = Array.Empty<FileScopeEntry>();

    /// <summary>
    /// Execution steps to perform.
    /// </summary>
    public IReadOnlyList<PlanStep> Steps { get; init; } = Array.Empty<PlanStep>();

    /// <summary>
    /// Verification steps to confirm task completion.
    /// </summary>
    public IReadOnlyList<VerificationStepEntry> VerificationSteps { get; init; } = Array.Empty<VerificationStepEntry>();
}

/// <summary>
/// Represents a file scope entry in a task plan.
/// </summary>
public sealed class FileScopeEntry
{
    /// <summary>
    /// The relative path to the file or directory.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// The type of scope (read, write, create, modify, delete).
    /// </summary>
    public string ScopeType { get; init; } = "read";

    /// <summary>
    /// Description of what will be done with this file.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Represents a step in the task plan.
/// </summary>
public sealed class PlanStep
{
    /// <summary>
    /// The step identifier.
    /// </summary>
    public string StepId { get; init; } = string.Empty;

    /// <summary>
    /// The type of step (create_file, modify_file, delete_file, run_command, etc.).
    /// </summary>
    public string StepType { get; init; } = string.Empty;

    /// <summary>
    /// The target file path for file-related steps.
    /// </summary>
    public string? TargetPath { get; init; }

    /// <summary>
    /// Description of what this step does.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Represents a verification step entry.
/// </summary>
public sealed class VerificationStepEntry
{
    /// <summary>
    /// The type of verification (test, compile, lint, manual_review).
    /// </summary>
    public string VerificationType { get; init; } = string.Empty;

    /// <summary>
    /// Description of what needs to be verified.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}
