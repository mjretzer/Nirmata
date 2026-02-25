using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gmsd.Agents.Execution.Brownfield.FileGraphBuilder;

/// <summary>
/// Writes file graph JSON output for the codebase intelligence pack.
/// Produces deterministic, canonical JSON output for file dependency graph.
/// </summary>
public sealed class FileGraphJsonWriter
{
    private readonly JsonSerializerOptions _serializerOptions;

    /// <summary>
    /// Creates a new file graph JSON writer with default serialization options.
    /// </summary>
    public FileGraphJsonWriter()
    {
        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };
    }

    /// <summary>
    /// Creates a new file graph JSON writer with custom serialization options.
    /// </summary>
    public FileGraphJsonWriter(JsonSerializerOptions serializerOptions)
    {
        _serializerOptions = serializerOptions ?? throw new ArgumentNullException(nameof(serializerOptions));
    }

    /// <summary>
    /// Writes the file graph result to a JSON file.
    /// </summary>
    /// <param name="result">The file graph result containing nodes and edges.</param>
    /// <param name="outputPath">The path to write the JSON file (typically .aos/codebase/cache/file-graph.json).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The path of the written file.</returns>
    public async Task<string> WriteAsync(FileGraphResult result, string outputPath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(outputPath);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException("Cannot write file graph for failed build: " + result.ErrorMessage);
        }

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var jsonModel = CreateDeterministicJsonModel(result);
        var json = JsonSerializer.Serialize(jsonModel, _serializerOptions);

        await File.WriteAllTextAsync(outputPath, json, ct);

        return outputPath;
    }

    private FileGraphJsonModel CreateDeterministicJsonModel(FileGraphResult result)
    {
        return new FileGraphJsonModel
        {
            Version = "1.0",
            Schema = "file-graph",
            RepositoryRoot = result.RepositoryRoot,
            BuildTimestamp = result.BuildTimestamp,
            Statistics = new GraphStatisticsModel
            {
                TotalNodes = result.Statistics.TotalNodes,
                TotalEdges = result.Statistics.TotalEdges,
                ProjectCount = result.Statistics.ProjectCount,
                SourceFileCount = result.Statistics.SourceFileCount,
                ProjectReferenceCount = result.Statistics.ProjectReferenceCount,
                ImportDependencyCount = result.Statistics.ImportDependencyCount,
                TypeReferenceCount = result.Statistics.TypeReferenceCount,
                AverageEdgeWeight = Math.Round(result.Statistics.AverageEdgeWeight, 3),
                BuildDurationMs = (long)result.Statistics.BuildDuration.TotalMilliseconds
            },
            Nodes = result.Nodes
                .OrderBy(n => n.Id)
                .Select(n => new NodeModel
                {
                    Id = n.Id,
                    FullPath = n.FullPath,
                    RelativePath = n.RelativePath,
                    NodeType = n.NodeType.ToString(),
                    ProjectId = n.ProjectId,
                    Extension = n.Extension,
                    FileSize = n.FileSize,
                    LastModified = n.LastModified
                })
                .ToList(),
            Edges = result.Edges
                .OrderBy(e => e.Id)
                .Select(e => new EdgeModel
                {
                    Id = e.Id,
                    SourceId = e.SourceId,
                    TargetId = e.TargetId,
                    EdgeType = e.EdgeType.ToString(),
                    ImportSymbol = e.ImportSymbol,
                    LineNumber = e.LineNumber,
                    Weight = e.Weight
                })
                .ToList()
        };
    }
}

/// <summary>
/// JSON model for file graph serialization.
/// </summary>
public sealed class FileGraphJsonModel
{
    public string Version { get; set; } = "1.0";
    public string Schema { get; set; } = "file-graph";
    public string RepositoryRoot { get; set; } = "";
    public DateTimeOffset BuildTimestamp { get; set; }
    public GraphStatisticsModel Statistics { get; set; } = new();
    public List<NodeModel> Nodes { get; set; } = new();
    public List<EdgeModel> Edges { get; set; } = new();
}

/// <summary>
/// JSON model for graph statistics.
/// </summary>
public sealed class GraphStatisticsModel
{
    public int TotalNodes { get; set; }
    public int TotalEdges { get; set; }
    public int ProjectCount { get; set; }
    public int SourceFileCount { get; set; }
    public int ProjectReferenceCount { get; set; }
    public int ImportDependencyCount { get; set; }
    public int TypeReferenceCount { get; set; }
    public double AverageEdgeWeight { get; set; }
    public long BuildDurationMs { get; set; }
}

/// <summary>
/// JSON model for a graph node.
/// </summary>
public sealed class NodeModel
{
    public string Id { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string NodeType { get; set; } = "";
    public string? ProjectId { get; set; }
    public string Extension { get; set; } = "";
    public long FileSize { get; set; }
    public DateTimeOffset LastModified { get; set; }
}

/// <summary>
/// JSON model for a graph edge.
/// </summary>
public sealed class EdgeModel
{
    public string Id { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string EdgeType { get; set; } = "";
    public string? ImportSymbol { get; set; }
    public int? LineNumber { get; set; }
    public double Weight { get; set; }
}
