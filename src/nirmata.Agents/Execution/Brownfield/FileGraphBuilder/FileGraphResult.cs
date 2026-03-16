namespace nirmata.Agents.Execution.Brownfield.FileGraphBuilder;

/// <summary>
/// Result of a file graph build operation.
/// </summary>
public sealed class FileGraphResult
{
    /// <summary>
    /// Whether the build completed successfully.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if the build failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The root path of the analyzed repository.
    /// </summary>
    public string RepositoryRoot { get; init; } = "";

    /// <summary>
    /// Timestamp when the graph was built.
    /// </summary>
    public DateTimeOffset BuildTimestamp { get; init; }

    /// <summary>
    /// All nodes in the file graph (files and projects).
    /// </summary>
    public IReadOnlyList<FileNode> Nodes { get; init; } = Array.Empty<FileNode>();

    /// <summary>
    /// All edges in the file graph (dependencies and relationships).
    /// </summary>
    public IReadOnlyList<FileEdge> Edges { get; init; } = Array.Empty<FileEdge>();

    /// <summary>
    /// Statistics about the file graph.
    /// </summary>
    public FileGraphStatistics Statistics { get; init; } = new();
}

/// <summary>
/// Represents a node in the file graph (source file or project).
/// </summary>
public sealed class FileNode
{
    /// <summary>
    /// Unique identifier for the node (relative path from repository root).
    /// </summary>
    public string Id { get; init; } = "";

    /// <summary>
    /// Full path to the file or project.
    /// </summary>
    public string FullPath { get; init; } = "";

    /// <summary>
    /// Relative path from repository root.
    /// </summary>
    public string RelativePath { get; init; } = "";

    /// <summary>
    /// Type of node (project file, source file, etc.).
    /// </summary>
    public NodeType NodeType { get; init; }

    /// <summary>
    /// For source files: the project this file belongs to.
    /// </summary>
    public string? ProjectId { get; init; }

    /// <summary>
    /// File extension (e.g., ".cs", ".csproj").
    /// </summary>
    public string Extension { get; init; } = "";

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; init; }

    /// <summary>
    /// Last modified timestamp.
    /// </summary>
    public DateTimeOffset LastModified { get; init; }
}

/// <summary>
/// Represents an edge in the file graph (dependency or relationship).
/// </summary>
public sealed class FileEdge
{
    /// <summary>
    /// Unique identifier for the edge.
    /// </summary>
    public string Id { get; init; } = "";

    /// <summary>
    /// Source node ID (the file that has the dependency).
    /// </summary>
    public string SourceId { get; init; } = "";

    /// <summary>
    /// Target node ID (the file being depended on).
    /// </summary>
    public string TargetId { get; init; } = "";

    /// <summary>
    /// Type of dependency relationship.
    /// </summary>
    public EdgeType EdgeType { get; init; }

    /// <summary>
    /// Weight representing coupling strength (higher = stronger coupling).
    /// </summary>
    public double Weight { get; init; }

    /// <summary>
    /// For import dependencies: the namespace or type being imported.
    /// </summary>
    public string? ImportSymbol { get; init; }

    /// <summary>
    /// Line number where the dependency is declared (if applicable).
    /// </summary>
    public int? LineNumber { get; init; }
}

/// <summary>
/// Type of node in the file graph.
/// </summary>
public enum NodeType
{
    Project,
    SourceFile,
    ConfigFile,
    ResourceFile
}

/// <summary>
/// Type of edge/dependency in the file graph.
/// </summary>
public enum EdgeType
{
    ProjectReference,
    ImportDependency,
    TypeReference,
    FileInclude
}

/// <summary>
/// Statistics about the file graph.
/// </summary>
public sealed class FileGraphStatistics
{
    /// <summary>
    /// Total number of nodes in the graph.
    /// </summary>
    public int TotalNodes { get; init; }

    /// <summary>
    /// Total number of edges in the graph.
    /// </summary>
    public int TotalEdges { get; init; }

    /// <summary>
    /// Number of project nodes.
    /// </summary>
    public int ProjectCount { get; init; }

    /// <summary>
    /// Number of source file nodes.
    /// </summary>
    public int SourceFileCount { get; init; }

    /// <summary>
    /// Number of project reference edges.
    /// </summary>
    public int ProjectReferenceCount { get; init; }

    /// <summary>
    /// Number of import dependency edges.
    /// </summary>
    public int ImportDependencyCount { get; init; }

    /// <summary>
    /// Number of type reference edges.
    /// </summary>
    public int TypeReferenceCount { get; init; }

    /// <summary>
    /// Average edge weight.
    /// </summary>
    public double AverageEdgeWeight { get; init; }

    /// <summary>
    /// Time taken to build the graph.
    /// </summary>
    public TimeSpan BuildDuration { get; init; }
}
