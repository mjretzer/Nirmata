namespace nirmata.Agents.Execution.Brownfield.CodebaseScanner;

/// <summary>
/// Request model for codebase scanning.
/// </summary>
public sealed class CodebaseScanRequest
{
    /// <summary>
    /// The root path of the repository to scan.
    /// If null, uses the current working directory.
    /// </summary>
    public string? RepositoryPath { get; init; }

    /// <summary>
    /// Options to configure the scan behavior.
    /// </summary>
    public CodebaseScanOptions Options { get; init; } = new();
}

/// <summary>
/// Options for configuring codebase scan behavior.
/// </summary>
public sealed class CodebaseScanOptions
{
    /// <summary>
    /// Include hidden directories (starting with '.') in the scan.
    /// Default is false.
    /// </summary>
    public bool IncludeHiddenDirectories { get; init; }

    /// <summary>
    /// Maximum depth to scan directory structure.
    /// Default is 0 (unlimited).
    /// </summary>
    public int MaxDepth { get; init; }

    /// <summary>
    /// File patterns to exclude from the scan.
    /// </summary>
    public IReadOnlyList<string> ExcludePatterns { get; init; } = new List<string>
    {
        "bin/",
        "obj/",
        "node_modules/",
        ".git/",
        "*.tmp",
        "*.log"
    };

    /// <summary>
    /// Enable parallel processing for large repositories.
    /// Default is true.
    /// </summary>
    public bool EnableParallelProcessing { get; init; } = true;

    /// <summary>
    /// Maximum files to scan before stopping.
    /// Default is 0 (unlimited).
    /// </summary>
    public int MaxFiles { get; init; }

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
    /// Path to cache directory for storing intermediate scan results.
    /// If null, caching is disabled.
    /// </summary>
    public string? CacheDirectoryPath { get; init; }

    /// <summary>
    /// Enable caching of intermediate scan results.
    /// Default is true.
    /// </summary>
    public bool EnableCaching { get; init; } = true;
}
