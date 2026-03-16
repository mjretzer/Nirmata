using System.ComponentModel.DataAnnotations;

namespace nirmata.Agents.Execution.ControlPlane;

/// <summary>
/// Represents a structured proposed action from the gating engine.
/// Uses DataAnnotations for validation and LLM structured output forcing.
/// </summary>
public sealed class ProposedAction
{
    /// <summary>
    /// The phase that will be executed (e.g., "Interviewer", "Planner", "Executor").
    /// </summary>
    [Required(ErrorMessage = "Phase is required")]
    [MinLength(1, ErrorMessage = "Phase cannot be empty")]
    [MaxLength(50, ErrorMessage = "Phase cannot exceed 50 characters")]
    [RegularExpression("^[A-Za-z]+$", ErrorMessage = "Phase must contain only letters")]
    public required string Phase { get; init; }

    /// <summary>
    /// Human-readable description of what will be done.
    /// </summary>
    [Required(ErrorMessage = "Description is required")]
    [MinLength(10, ErrorMessage = "Description must be at least 10 characters")]
    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public required string Description { get; init; }

    /// <summary>
    /// The risk level of the operation (read, write-safe, write-destructive).
    /// </summary>
    [Required(ErrorMessage = "RiskLevel is required")]
    [EnumDataType(typeof(RiskLevel), ErrorMessage = "Invalid risk level")]
    public required RiskLevel RiskLevel { get; init; }

    /// <summary>
    /// The types of side effects this operation may have (file system, database, external API, etc.).
    /// </summary>
    [MaxLength(10, ErrorMessage = "Cannot have more than 10 side effect types")]
    public IReadOnlyList<string> SideEffects { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Resource identifiers that will be affected (file paths, DB entities, etc.).
    /// </summary>
    [Required(ErrorMessage = "At least one AffectedResource is required")]
    [MinLength(1, ErrorMessage = "At least one affected resource must be specified")]
    [MaxLength(100, ErrorMessage = "Cannot have more than 100 affected resources")]
    public IReadOnlyList<string> AffectedResources { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Additional metadata about the action.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Estimated impact scope (number of files, records, etc.).
    /// </summary>
    [MaxLength(100, ErrorMessage = "EstimatedImpact cannot exceed 100 characters")]
    public string? EstimatedImpact { get; init; }

    /// <summary>
    /// Unique identifier for this proposed action (for deduplication and tracking).
    /// </summary>
    public string ActionId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Timestamp when this action was proposed.
    /// </summary>
    public DateTimeOffset ProposedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Validates the proposed action using DataAnnotations and custom rules.
    /// </summary>
    /// <returns>Validation result indicating if the action is valid.</returns>
    public ValidationResult Validate()
    {
        var errors = new List<string>();

        // DataAnnotations validation
        var validationContext = new ValidationContext(this);
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        bool isDataAnnotationsValid = Validator.TryValidateObject(
            this, validationContext, validationResults, validateAllProperties: true);

        if (!isDataAnnotationsValid)
        {
            errors.AddRange(validationResults.Select(vr => vr.ErrorMessage ?? "Validation failed"));
        }

        // Ensure affected resources are within scope (security check)
        foreach (var resource in AffectedResources)
        {
            if (string.IsNullOrWhiteSpace(resource))
            {
                errors.Add("Affected resources cannot contain empty entries");
                break;
            }

            // Check for potentially dangerous paths
            if (resource.Contains("..") || resource.StartsWith("/etc/") || resource.StartsWith("C:\\Windows"))
            {
                errors.Add($"Resource '{resource}' is outside allowed scope");
            }
        }

        // Verify risk level matches phase characteristics (warning only)
        var expectedRisk = GetExpectedRiskForPhase(Phase);
        if (expectedRisk.HasValue && RiskLevel != expectedRisk.Value)
        {
            // This is a warning-level issue, logged but not blocking
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }

    /// <summary>
    /// Validates completeness - stricter validation for server-side use.
    /// Ensures all required fields for user confirmation are present.
    /// </summary>
    /// <returns>Validation result with completeness check.</returns>
    public ValidationResult ValidateCompleteness()
    {
        var errors = new List<string>();

        // Basic validation first
        var basicValidation = Validate();
        if (!basicValidation.IsValid)
        {
            errors.AddRange(basicValidation.Errors);
        }

        // Completeness checks for user confirmation
        if (string.IsNullOrWhiteSpace(Description) || Description.Length < 20)
        {
            errors.Add("Description must be at least 20 characters for user confirmation");
        }

        if (AffectedResources.Count == 0 || AffectedResources.All(string.IsNullOrWhiteSpace))
        {
            errors.Add("At least one valid affected resource must be specified for user confirmation");
        }

        if (RiskLevel == RiskLevel.Read && (SideEffects?.Any(s => s.Contains("write", StringComparison.OrdinalIgnoreCase)) ?? false))
        {
            errors.Add("RiskLevel is Read but side effects indicate write operations - inconsistency detected");
        }

        // High-risk operations need more detailed descriptions
        if ((RiskLevel == RiskLevel.WriteDestructive || RiskLevel == RiskLevel.WriteDestructiveGit || RiskLevel == RiskLevel.WorkspaceDestructive)
            && Description.Length < 30)
        {
            errors.Add("Destructive operations require a description of at least 30 characters");
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }

    /// <summary>
    /// Gets the expected risk level for a given phase.
    /// </summary>
    private static RiskLevel? GetExpectedRiskForPhase(string? phase)
    {
        return phase?.ToLowerInvariant() switch
        {
            "interviewer" or "roadmapper" or "planner" => RiskLevel.WriteSafe,
            "executor" => RiskLevel.WriteDestructive,
            "verifier" or "responder" => RiskLevel.Read,
            _ => null
        };
    }
}

/// <summary>
/// Result of validating a proposed action.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Whether the validation passed.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// List of validation errors if any.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static ValidationResult Failure(IEnumerable<string> errors) =>
        new() { IsValid = false, Errors = errors.ToList() };
}

/// <summary>
/// Risk levels for operations.
/// </summary>
public enum RiskLevel
{
    /// <summary>
    /// Read-only operations that don't modify state.
    /// </summary>
    Read,

    /// <summary>
    /// Write operations that are safe and reversible.
    /// </summary>
    WriteSafe,

    /// <summary>
    /// Write operations that are destructive or hard to undo.
    /// </summary>
    WriteDestructive,

    /// <summary>
    /// Git operations that are irreversible (commits, pushes).
    /// </summary>
    WriteDestructiveGit,

    /// <summary>
    /// Operations that could delete or corrupt workspace state.
    /// </summary>
    WorkspaceDestructive
}
