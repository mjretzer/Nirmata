using Gmsd.Agents.Execution.Planning.PhasePlanner.Assumptions;
using Gmsd.Agents.Execution.Planning.PhasePlanner.ContextGatherer;
using Gmsd.Aos.Contracts.Commands;

namespace Gmsd.Agents.Execution.Planning.PhasePlanner;

/// <summary>
/// Result of phase planning workflow execution.
/// </summary>
public sealed record PhasePlannerResult
{
    /// <summary>
    /// Whether the planning completed successfully.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// The phase identifier that was planned.
    /// </summary>
    public string PhaseId { get; init; } = string.Empty;

    /// <summary>
    /// The generated task plan.
    /// </summary>
    public TaskPlan? TaskPlan { get; init; }

    /// <summary>
    /// The phase brief used for planning.
    /// </summary>
    public PhaseBrief? PhaseBrief { get; init; }

    /// <summary>
    /// The assumptions identified during planning.
    /// </summary>
    public IReadOnlyList<PhaseAssumption> Assumptions { get; init; } = Array.Empty<PhaseAssumption>();

    /// <summary>
    /// Path to the generated assumptions document.
    /// </summary>
    public string? AssumptionsDocumentPath { get; init; }

    /// <summary>
    /// Error message if planning failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Artifacts generated during planning.
    /// </summary>
    public IReadOnlyList<PlanningArtifact> Artifacts { get; init; } = Array.Empty<PlanningArtifact>();

    /// <summary>
    /// When planning started.
    /// </summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// When planning completed.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; init; }
}

/// <summary>
/// Represents an artifact generated during phase planning.
/// </summary>
public sealed record PlanningArtifact
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
    /// The type of artifact (brief, plan, assumptions, decision).
    /// </summary>
    public required string ArtifactType { get; init; }
}
