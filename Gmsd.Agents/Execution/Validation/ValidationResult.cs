namespace Gmsd.Agents.Execution.Validation;

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Gets a value indicating whether all validation checks passed.
    /// </summary>
    public bool IsValid => !Issues.Any(i => i.Severity == ValidationSeverity.Error);

    /// <summary>
    /// Gets the list of validation issues found.
    /// </summary>
    public IReadOnlyList<ValidationIssue> Issues { get; init; } = Array.Empty<ValidationIssue>();

    /// <summary>
    /// A successful validation result with no issues.
    /// </summary>
    public static ValidationResult Success { get; } = new();
}

/// <summary>
/// Represents a single validation issue.
/// </summary>
public sealed class ValidationIssue
{
    /// <summary>
    /// Gets the type of issue (e.g., "SchemaViolation", "MissingFile").
    /// </summary>
    public required string IssueType { get; init; }

    /// <summary>
    /// Gets the human-readable description of the issue.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the severity of the issue.
    /// </summary>
    public ValidationSeverity Severity { get; init; }
}

/// <summary>
/// Defines the severity levels for validation issues.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// An informational message.
    /// </summary>
    Info,

    /// <summary>
    /// A warning that does not block execution.
    /// </summary>
    Warning,

    /// <summary>
    /// An error that blocks execution.
    /// </summary>
    Error
}
