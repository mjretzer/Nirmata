namespace nirmata.Agents.Models.Contracts;

/// <summary>
/// Represents a milestone item for skeleton generation during roadmap creation.
/// </summary>
public sealed class MilestoneItem
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
    /// Phase items associated with this milestone.
    /// </summary>
    public List<PhaseItem> Phases { get; set; } = new();

    /// <summary>
    /// Sequence order for this milestone in the roadmap.
    /// </summary>
    public int SequenceOrder { get; set; }

    /// <summary>
    /// Expected completion criteria for this milestone.
    /// </summary>
    public List<string> CompletionCriteria { get; set; } = new();
}
