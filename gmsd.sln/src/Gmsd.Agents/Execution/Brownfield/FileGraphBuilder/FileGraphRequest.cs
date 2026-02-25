namespace Gmsd.Agents.Execution.Brownfield.FileGraphBuilder;

/// <summary>
/// Request model for file graph building.
/// </summary>
public sealed class FileGraphRequest
{
    /// <summary>
    /// The root path of the repository to analyze.
    /// </summary>
    public string RepositoryPath { get; init; } = "";

    /// <summary>
    /// Optional: Limit to specific project files only (full paths to .csproj files).
    /// If empty, all projects in the repository are analyzed.
    /// </summary>
    public IReadOnlyList<string> ProjectFiles { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Options to configure the file graph building behavior.
    /// </summary>
    public FileGraphOptions Options { get; init; } = new();
}

/// <summary>
/// Options for configuring file graph building.
/// </summary>
public sealed class FileGraphOptions
{
    /// <summary>
    /// Include project-level references in the graph.
    /// Default is true.
    /// </summary>
    public bool IncludeProjectReferences { get; init; } = true;

    /// <summary>
    /// Include using/import directive dependencies in the graph.
    /// Default is true.
    /// </summary>
    public bool IncludeImportDependencies { get; init; } = true;

    /// <summary>
    /// Include file-to-file relationships within the same project.
    /// Default is true.
    /// </summary>
    public bool IncludeIntraProjectEdges { get; init; } = true;

    /// <summary>
    /// Calculate edge weights based on coupling strength.
    /// Default is true.
    /// </summary>
    public bool CalculateEdgeWeights { get; init; } = true;

    /// <summary>
    /// File patterns to exclude from graph analysis.
    /// </summary>
    public IReadOnlyList<string> ExcludePatterns { get; init; } = new List<string>
    {
        "bin/",
        "obj/",
        "node_modules/",
        ".git/"
    };

    /// <summary>
    /// Enable parallel processing for large repositories.
    /// Default is true.
    /// </summary>
    public bool EnableParallelProcessing { get; init; } = true;

    /// <summary>
    /// Enable incremental scanning - only scan files that have changed since last scan.
    /// Requires <see cref="PreviousScanTimestamp"/> to be set.
    /// Default is false.
    /// </summary>
    public bool EnableIncrementalScan { get; init; }

    /// <summary>
    /// Timestamp of the previous scan for incremental scanning.
    /// Files modified after this timestamp will be rescanned.
    /// </summary>
    public DateTimeOffset? PreviousScanTimestamp { get; init; }

    /// <summary>
    /// Enable caching of intermediate graph building results.
    /// Default is true.
    /// </summary>
    public bool EnableCaching { get; init; } = true;

    /// <summary>
    /// Path to cache directory for storing intermediate results.
    /// If null, a default temp directory is used.
    /// </summary>
    public string? CacheDirectoryPath { get; init; }

    /// <summary>
    /// Maximum depth for dependency chain traversal.
    /// Default is 10.
    /// </summary>
    public int MaxDependencyDepth { get; init; } = 10;
}
