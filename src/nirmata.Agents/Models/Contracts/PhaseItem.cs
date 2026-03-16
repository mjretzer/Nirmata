namespace nirmata.Agents.Models.Contracts;

/// <summary>
/// Represents a phase item for skeleton generation during roadmap creation.
/// </summary>
public sealed class PhaseItem
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
    /// The milestone ID this phase belongs to.
    /// </summary>
    public string MilestoneId { get; set; } = string.Empty;

    /// <summary>
    /// Sequence order for this phase within the milestone.
    /// </summary>
    public int SequenceOrder { get; set; }

    /// <summary>
    /// Expected deliverables for this phase.
    /// </summary>
    public List<string> Deliverables { get; set; } = new();

    /// <summary>
    /// Input artifacts required for this phase.
    /// </summary>
    public List<string> InputArtifacts { get; set; } = new();

    /// <summary>
    /// Output artifacts produced by this phase.
    /// </summary>
    public List<string> OutputArtifacts { get; set; } = new();
}
