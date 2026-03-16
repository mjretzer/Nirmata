namespace nirmata.Agents.Execution.Brownfield.MapValidator;

/// <summary>
/// Result of a map validation operation.
/// </summary>
public sealed class MapValidationResult
{
    /// <summary>
    /// Whether all validation checks passed.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// List of validation issues found.
    /// </summary>
    public IReadOnlyList<MapValidationIssue> Issues { get; init; } = Array.Empty<MapValidationIssue>();

    /// <summary>
    /// Summary statistics about the validation.
    /// </summary>
    public MapValidationSummary Summary { get; init; } = new();
}

/// <summary>
/// A single validation issue.
/// </summary>
public sealed class MapValidationIssue
{
    /// <summary>
    /// Type of issue (SchemaViolation, MissingFile, CrossFileInvariant, etc.).
    /// </summary>
    public string IssueType { get; init; } = "";

    /// <summary>
    /// The artifact file where the issue was found (e.g., "map.json", "symbols.json").
    /// </summary>
    public string Artifact { get; init; } = "";

    /// <summary>
    /// JSON path or location where the issue was found.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Human-readable description of the issue.
    /// </summary>
    public string Message { get; init; } = "";

    /// <summary>
    /// Severity of the issue (Error, Warning, Info).
    /// </summary>
    public ValidationSeverity Severity { get; init; }
}

/// <summary>
/// Severity levels for validation issues.
/// </summary>
public enum ValidationSeverity
{
    Error,
    Warning,
    Info
}

/// <summary>
/// Summary statistics for validation.
/// </summary>
public sealed class MapValidationSummary
{
    /// <summary>
    /// Number of artifacts validated.
    /// </summary>
    public int ArtifactsValidated { get; init; }

    /// <summary>
    /// Number of error-level issues.
    /// </summary>
    public int ErrorCount { get; init; }

    /// <summary>
    /// Number of warning-level issues.
    /// </summary>
    public int WarningCount { get; init; }

    /// <summary>
    /// Number of info-level issues.
    /// </summary>
    public int InfoCount { get; init; }

    /// <summary>
    /// Timestamp when validation completed.
    /// </summary>
    public DateTimeOffset ValidationTimestamp { get; init; }
}
