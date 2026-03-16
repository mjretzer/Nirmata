namespace nirmata.Agents.Execution.Brownfield.SymbolCacheBuilder;

/// <summary>
/// Request model for symbol cache building.
/// </summary>
public sealed class SymbolCacheRequest
{
    /// <summary>
    /// The root path of the repository to scan for symbols.
    /// </summary>
    public string RepositoryPath { get; init; } = "";

    /// <summary>
    /// Optional: Limit to specific source files only (full paths).
    /// If empty, all source files in the repository are scanned.
    /// </summary>
    public IReadOnlyList<string> SourceFiles { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Options to configure the symbol extraction behavior.
    /// </summary>
    public SymbolCacheOptions Options { get; init; } = new();
}

/// <summary>
/// Options for configuring symbol cache building.
/// </summary>
public sealed class SymbolCacheOptions
{
    /// <summary>
    /// Include private symbols (private methods, fields, etc.).
    /// Default is true.
    /// </summary>
    public bool IncludePrivateSymbols { get; init; } = true;

    /// <summary>
    /// Include internal symbols.
    /// Default is true.
    /// </summary>
    public bool IncludeInternalSymbols { get; init; } = true;

    /// <summary>
    /// Include documentation comments as symbol metadata.
    /// Default is true.
    /// </summary>
    public bool IncludeDocumentation { get; init; } = true;

    /// <summary>
    /// File patterns to exclude from symbol extraction.
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
    /// Enable caching of intermediate symbol extraction results.
    /// Default is true.
    /// </summary>
    public bool EnableCaching { get; init; } = true;

    /// <summary>
    /// Path to cache directory for storing intermediate results.
    /// If null, a default temp directory is used.
    /// </summary>
    public string? CacheDirectoryPath { get; init; }

    /// <summary>
    /// Maximum depth for cross-reference detection (to avoid infinite recursion).
    /// Default is 10.
    /// </summary>
    public int MaxCrossReferenceDepth { get; init; } = 10;
}
