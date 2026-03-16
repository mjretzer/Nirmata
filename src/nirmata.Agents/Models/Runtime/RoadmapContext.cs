namespace nirmata.Agents.Models.Runtime;

/// <summary>
/// Execution context for the Roadmapper workflow.
/// </summary>
public sealed class RoadmapContext
{
    /// <summary>
    /// Unique identifier for the run.
    /// </summary>
    public string RunId { get; set; } = string.Empty;

    /// <summary>
    /// Absolute path to the workspace directory.
    /// </summary>
    public string WorkspacePath { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the validated project specification.
    /// </summary>
    public ProjectSpecReference ProjectSpec { get; set; } = new();

    /// <summary>
    /// Cancellation token for the operation.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>
    /// Optional correlation ID for tracing.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Metadata for the roadmap generation.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Reference to a validated project specification.
/// </summary>
public sealed class ProjectSpecReference
{
    /// <summary>
    /// The path to the project spec file (typically .aos/spec/project.json).
    /// </summary>
    public string SpecPath { get; set; } = string.Empty;

    /// <summary>
    /// The project identifier from the spec.
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// The project name from the spec.
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// The semantic version of the project spec schema.
    /// </summary>
    public string SchemaVersion { get; set; } = string.Empty;
}
