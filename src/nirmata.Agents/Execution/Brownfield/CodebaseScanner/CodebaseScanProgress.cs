namespace nirmata.Agents.Execution.Brownfield.CodebaseScanner;

/// <summary>
/// Represents the progress of a codebase scan operation.
/// </summary>
public sealed record CodebaseScanProgress
{
    /// <summary>
    /// The current phase of the scan operation.
    /// </summary>
    public CodebaseScanPhase Phase { get; init; }

    /// <summary>
    /// The current step number (1-based).
    /// </summary>
    public int StepNumber { get; init; }

    /// <summary>
    /// The total number of steps in the scan.
    /// </summary>
    public int TotalSteps { get; init; }

    /// <summary>
    /// Percentage completion (0-100).
    /// </summary>
    public int PercentComplete { get; init; }

    /// <summary>
    /// Human-readable description of current operation.
    /// </summary>
    public string Message { get; init; } = "";

    /// <summary>
    /// Number of items processed in current phase (e.g., files scanned).
    /// </summary>
    public int ItemsProcessed { get; init; }

    /// <summary>
    /// Total number of items to process in current phase.
    /// </summary>
    public int TotalItems { get; init; }
}

/// <summary>
/// Phases of the codebase scan operation.
/// </summary>
public enum CodebaseScanPhase
{
    /// <summary>
    /// Initial phase - detecting repository root.
    /// </summary>
    DetectingRepository,

    /// <summary>
    /// Discovering solution files.
    /// </summary>
    DiscoveringSolutions,

    /// <summary>
    /// Loading project information from project files.
    /// </summary>
    LoadingProjects,

    /// <summary>
    /// Building directory structure tree.
    /// </summary>
    BuildingStructure,

    /// <summary>
    /// Analyzing technology stack.
    /// </summary>
    AnalyzingStack,

    /// <summary>
    /// Calculating repository statistics.
    /// </summary>
    CalculatingStatistics,

    /// <summary>
    /// Scan completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Scan failed.
    /// </summary>
    Failed
}
