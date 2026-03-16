namespace nirmata.Agents.Models.Results;

/// <summary>
/// Encapsulates the result of roadmap generation.
/// </summary>
public sealed class RoadmapResult
{
    /// <summary>
    /// Whether the roadmap generation completed successfully.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// The generated roadmap specification identifier.
    /// </summary>
    public string RoadmapId { get; set; } = string.Empty;

    /// <summary>
    /// The path to the generated roadmap spec file.
    /// </summary>
    public string RoadmapSpecPath { get; set; } = string.Empty;

    /// <summary>
    /// Collection of generated milestone specifications.
    /// </summary>
    public List<MilestoneSpec> MilestoneSpecs { get; set; } = new();

    /// <summary>
    /// Collection of generated phase specifications.
    /// </summary>
    public List<PhaseSpec> PhaseSpecs { get; set; } = new();

    /// <summary>
    /// Error message if generation failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Error code if generation failed.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Timestamp when the roadmap generation started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Timestamp when the roadmap generation completed.
    /// </summary>
    public DateTimeOffset CompletedAt { get; set; }

    /// <summary>
    /// Duration of the roadmap generation.
    /// </summary>
    public TimeSpan Duration => CompletedAt - StartedAt;
}

/// <summary>
/// Specification for a generated milestone.
/// </summary>
public sealed class MilestoneSpec
{
    /// <summary>
    /// Unique identifier for the milestone (e.g., MS-0001).
    /// </summary>
    public string MilestoneId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name for the milestone.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the milestone objectives.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Path to the milestone spec file.
    /// </summary>
    public string SpecPath { get; set; } = string.Empty;

    /// <summary>
    /// Phase identifiers associated with this milestone.
    /// </summary>
    public List<string> PhaseIds { get; set; } = new();
}

/// <summary>
/// Specification for a generated phase.
/// </summary>
public sealed class PhaseSpec
{
    /// <summary>
    /// Unique identifier for the phase (e.g., PH-0001).
    /// </summary>
    public string PhaseId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name for the phase.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the phase objectives.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Path to the phase spec file.
    /// </summary>
    public string SpecPath { get; set; } = string.Empty;

    /// <summary>
    /// The milestone this phase belongs to.
    /// </summary>
    public string MilestoneId { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the phase (pending, active, completed).
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Expected sequence order for execution.
    /// </summary>
    public int SequenceOrder { get; set; }
}
