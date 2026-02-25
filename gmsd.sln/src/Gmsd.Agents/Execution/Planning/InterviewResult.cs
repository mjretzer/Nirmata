namespace Gmsd.Agents.Execution.Planning;

/// <summary>
/// Represents the result of a completed interview.
/// </summary>
public sealed record InterviewResult
{
    /// <summary>
    /// Whether the interview completed successfully.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// The generated project specification in JSON format.
    /// </summary>
    public string? ProjectSpecJson { get; init; }

    /// <summary>
    /// The normalized project specification object.
    /// </summary>
    public ProjectSpecification? ProjectSpec { get; init; }

    /// <summary>
    /// The full interview transcript as markdown.
    /// </summary>
    public string? TranscriptMarkdown { get; init; }

    /// <summary>
    /// A summary of key decisions and requirements.
    /// </summary>
    public string? SummaryMarkdown { get; init; }

    /// <summary>
    /// Error message if the interview failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The final interview session state.
    /// </summary>
    public InterviewSession? Session { get; init; }

    /// <summary>
    /// List of artifacts written by the interview.
    /// </summary>
    public IReadOnlyList<InterviewArtifact> Artifacts { get; init; } = Array.Empty<InterviewArtifact>();
}

/// <summary>
/// Represents a normalized project specification.
/// </summary>
public sealed record ProjectSpecification
{
    /// <summary>
    /// Schema identifier for this project spec.
    /// </summary>
    public string Schema { get; init; } = "gmsd:aos:schema:project:v1";

    /// <summary>
    /// The project name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The project description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The technology stack or primary language.
    /// </summary>
    public string? TechnologyStack { get; init; }

    /// <summary>
    /// Project goals.
    /// </summary>
    public IReadOnlyList<string> Goals { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Target audience description.
    /// </summary>
    public string? TargetAudience { get; init; }

    /// <summary>
    /// Key features identified.
    /// </summary>
    public IReadOnlyList<string> KeyFeatures { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Constraints and limitations.
    /// </summary>
    public IReadOnlyList<string> Constraints { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Assumptions made.
    /// </summary>
    public IReadOnlyList<string> Assumptions { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Additional metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// When the project spec was generated.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Represents an artifact produced by the interview process.
/// </summary>
public sealed record InterviewArtifact
{
    /// <summary>
    /// The artifact identifier.
    /// </summary>
    public required string ArtifactId { get; init; }

    /// <summary>
    /// The artifact file name.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// The absolute path to the artifact.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// The artifact content type.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// The artifact content.
    /// </summary>
    public string? Content { get; init; }
}
