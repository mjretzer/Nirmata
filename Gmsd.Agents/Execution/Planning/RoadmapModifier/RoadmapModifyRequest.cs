namespace Gmsd.Agents.Execution.Planning.RoadmapModifier;

/// <summary>
/// Defines the type of roadmap modification operation.
/// </summary>
public enum RoadmapModifyOperation
{
    /// <summary>
    /// Insert a new phase into the roadmap.
    /// </summary>
    Insert,

    /// <summary>
    /// Remove an existing phase from the roadmap.
    /// </summary>
    Remove,

    /// <summary>
    /// Renumber all phases to ensure consistent sequencing.
    /// </summary>
    Renumber
}

/// <summary>
/// Specifies the position for inserting a new phase.
/// </summary>
public enum InsertPosition
{
    /// <summary>
    /// Insert before the specified reference phase.
    /// </summary>
    Before,

    /// <summary>
    /// Insert after the specified reference phase.
    /// </summary>
    After,

    /// <summary>
    /// Insert at the beginning of the roadmap.
    /// </summary>
    AtBeginning,

    /// <summary>
    /// Insert at the end of the roadmap.
    /// </summary>
    AtEnd
}

/// <summary>
/// Encapsulates parameters for a roadmap modification request.
/// </summary>
public sealed class RoadmapModifyRequest
{
    /// <summary>
    /// The type of modification operation to perform.
    /// </summary>
    public RoadmapModifyOperation Operation { get; set; }

    /// <summary>
    /// For insert operations: the name of the new phase.
    /// </summary>
    public string? NewPhaseName { get; set; }

    /// <summary>
    /// For insert operations: the description of the new phase.
    /// </summary>
    public string? NewPhaseDescription { get; set; }

    /// <summary>
    /// For insert operations: the milestone ID to associate with the new phase.
    /// </summary>
    public string? TargetMilestoneId { get; set; }

    /// <summary>
    /// The reference phase ID for insert operations (used with InsertPosition).
    /// </summary>
    public string? ReferencePhaseId { get; set; }

    /// <summary>
    /// The position for inserting the new phase relative to the reference phase.
    /// </summary>
    public InsertPosition Position { get; set; }

    /// <summary>
    /// For remove operations: if true, allows removal of an active phase.
    /// </summary>
    public bool Force { get; set; }

    /// <summary>
    /// The path to the roadmap spec file to modify.
    /// </summary>
    public string RoadmapSpecPath { get; set; } = string.Empty;

    /// <summary>
    /// The path to the state file for cursor tracking.
    /// </summary>
    public string StatePath { get; set; } = string.Empty;
}
