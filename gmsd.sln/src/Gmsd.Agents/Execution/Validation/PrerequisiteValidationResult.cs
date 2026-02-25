using Gmsd.Agents.Execution.ControlPlane;

namespace Gmsd.Agents.Execution.Validation;

/// <summary>
/// Represents the result of a prerequisite validation check.
/// </summary>
public sealed class PrerequisiteValidationResult
{
    /// <summary>
    /// Whether all prerequisites are satisfied.
    /// </summary>
    public bool IsSatisfied { get; init; }

    /// <summary>
    /// The target phase that was validated.
    /// </summary>
    public required string TargetPhase { get; init; }

    /// <summary>
    /// The missing prerequisite if any.
    /// </summary>
    public MissingPrerequisiteDetail? Missing { get; init; }

    /// <summary>
    /// List of all checked prerequisites and their status.
    /// </summary>
    public IReadOnlyList<PrerequisiteStatus> CheckedPrerequisites { get; init; } = Array.Empty<PrerequisiteStatus>();

    /// <summary>
    /// Creates a satisfied result.
    /// </summary>
    public static PrerequisiteValidationResult Satisfied(string targetPhase, IEnumerable<PrerequisiteStatus>? checkedItems = null)
    {
        return new PrerequisiteValidationResult
        {
            IsSatisfied = true,
            TargetPhase = targetPhase,
            CheckedPrerequisites = checkedItems?.ToList() ?? new List<PrerequisiteStatus>()
        };
    }

    /// <summary>
    /// Creates a result with a missing prerequisite.
    /// </summary>
    public static PrerequisiteValidationResult NotSatisfied(string targetPhase, MissingPrerequisiteDetail missing, IEnumerable<PrerequisiteStatus>? checkedItems = null)
    {
        return new PrerequisiteValidationResult
        {
            IsSatisfied = false,
            TargetPhase = targetPhase,
            Missing = missing,
            CheckedPrerequisites = checkedItems?.ToList() ?? new List<PrerequisiteStatus>()
        };
    }
}

/// <summary>
/// Details about a missing prerequisite.
/// </summary>
public sealed class MissingPrerequisiteDetail
{
    /// <summary>
    /// The type of prerequisite that is missing.
    /// </summary>
    public required PrerequisiteType Type { get; init; }

    /// <summary>
    /// Human-readable description of what's missing.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The workspace path that should exist.
    /// </summary>
    public required string ExpectedPath { get; init; }

    /// <summary>
    /// Suggested recovery command to resolve the missing prerequisite.
    /// </summary>
    public string? SuggestedCommand { get; init; }

    /// <summary>
    /// Machine-readable failure code for diagnostics.
    /// </summary>
    public string? FailureCode { get; init; }

    /// <summary>
    /// Name of the prerequisite that failed readiness checks.
    /// </summary>
    public string? FailingPrerequisite { get; init; }

    /// <summary>
    /// Deterministic repair steps attempted before failure.
    /// </summary>
    public IReadOnlyList<string> AttemptedRepairs { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Actionable fixes to recover from the failure.
    /// </summary>
    public IReadOnlyList<string> SuggestedFixes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The recovery action description.
    /// </summary>
    public string? RecoveryAction { get; init; }

    /// <summary>
    /// Human-readable conversational prompt to present to the user.
    /// </summary>
    public string? ConversationalPrompt { get; init; }
}

/// <summary>
/// Types of prerequisites that can be checked.
/// </summary>
public enum PrerequisiteType
{
    /// <summary>
    /// The workspace itself is not initialized.
    /// </summary>
    Workspace,

    /// <summary>
    /// Project specification is missing.
    /// </summary>
    ProjectSpec,

    /// <summary>
    /// Roadmap is missing.
    /// </summary>
    Roadmap,

    /// <summary>
    /// Phase plan is missing for current cursor.
    /// </summary>
    Plan,

    /// <summary>
    /// State file is missing.
    /// </summary>
    State
}

/// <summary>
/// Status of a single prerequisite check.
/// </summary>
public sealed class PrerequisiteStatus
{
    /// <summary>
    /// The type of prerequisite.
    /// </summary>
    public required PrerequisiteType Type { get; init; }

    /// <summary>
    /// Whether the prerequisite exists and is valid.
    /// </summary>
    public required bool Exists { get; init; }

    /// <summary>
    /// The path that was checked.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Optional error message if the prerequisite is invalid.
    /// </summary>
    public string? Error { get; init; }
}

/// <summary>
/// Result of workspace bootstrap check.
/// </summary>
public sealed class WorkspaceBootstrapResult
{
    /// <summary>
    /// Whether the workspace is fully initialized.
    /// </summary>
    public bool IsInitialized { get; init; }

    /// <summary>
    /// Whether the .aos directory exists.
    /// </summary>
    public bool HasAosDirectory { get; init; }

    /// <summary>
    /// Whether the .aos/spec directory exists.
    /// </summary>
    public bool HasSpecDirectory { get; init; }

    /// <summary>
    /// Whether the .aos/state directory exists.
    /// </summary>
    public bool HasStateDirectory { get; init; }

    /// <summary>
    /// List of existing spec files found.
    /// </summary>
    public IReadOnlyList<string> FoundSpecFiles { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The suggested command to initialize the workspace.
    /// </summary>
    public string? BootstrapCommand { get; init; }

    /// <summary>
    /// Conversational prompt for workspace bootstrap.
    /// </summary>
    public string? BootstrapPrompt { get; init; }
}
