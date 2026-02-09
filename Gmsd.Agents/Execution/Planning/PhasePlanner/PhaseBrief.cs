namespace Gmsd.Agents.Execution.Planning.PhasePlanner;

/// <summary>
/// Represents a comprehensive brief about a phase to support task planning.
/// </summary>
public sealed record PhaseBrief
{
    /// <summary>
    /// The unique identifier of the phase being planned.
    /// </summary>
    public required string PhaseId { get; init; }

    /// <summary>
    /// The name of the phase.
    /// </summary>
    public required string PhaseName { get; init; }

    /// <summary>
    /// The description of the phase objectives.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The milestone ID this phase belongs to.
    /// </summary>
    public required string MilestoneId { get; init; }

    /// <summary>
    /// The goals to be achieved in this phase.
    /// </summary>
    public IReadOnlyList<string> Goals { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Constraints and limitations for this phase.
    /// </summary>
    public IReadOnlyList<string> Constraints { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The scope boundaries for this phase (what's in and out of scope).
    /// </summary>
    public PhaseScope Scope { get; init; } = new();

    /// <summary>
    /// Input artifacts required for this phase (from previous phases or specs).
    /// </summary>
    public IReadOnlyList<string> InputArtifacts { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Expected output artifacts from this phase.
    /// </summary>
    public IReadOnlyList<string> ExpectedOutputs { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Relevant code files identified for this phase.
    /// </summary>
    public IReadOnlyList<CodeFileReference> RelevantFiles { get; init; } = Array.Empty<CodeFileReference>();

    /// <summary>
    /// Project context including technology stack and conventions.
    /// </summary>
    public ProjectContext ProjectContext { get; init; } = new();

    /// <summary>
    /// When the brief was generated.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The run identifier that generated this brief.
    /// </summary>
    public string RunId { get; init; } = string.Empty;
}

/// <summary>
/// Defines the scope boundaries for a phase.
/// </summary>
public sealed record PhaseScope
{
    /// <summary>
    /// Items explicitly in scope for this phase.
    /// </summary>
    public IReadOnlyList<string> InScope { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Items explicitly out of scope for this phase.
    /// </summary>
    public IReadOnlyList<string> OutOfScope { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Boundaries or limitations of the scope.
    /// </summary>
    public IReadOnlyList<string> Boundaries { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Reference to a relevant code file for phase planning.
/// </summary>
public sealed record CodeFileReference
{
    /// <summary>
    /// The absolute path to the file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// The relative path from the workspace root.
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// Brief description of why this file is relevant.
    /// </summary>
    public string? Relevance { get; init; }

    /// <summary>
    /// The type of file (e.g., interface, implementation, test, config).
    /// </summary>
    public string? FileType { get; init; }
}

/// <summary>
/// Project context information for phase planning.
/// </summary>
public sealed record ProjectContext
{
    /// <summary>
    /// The primary technology stack.
    /// </summary>
    public string TechnologyStack { get; init; } = string.Empty;

    /// <summary>
    /// Project conventions (naming, patterns, etc.).
    /// </summary>
    public IReadOnlyList<string> Conventions { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Architecture patterns used in the project.
    /// </summary>
    public IReadOnlyList<string> ArchitecturePatterns { get; init; } = Array.Empty<string>();
}
