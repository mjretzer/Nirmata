namespace nirmata.Agents.Execution.Planning.RoadmapModifier;

/// <summary>
/// Defines the contract for safe roadmap modifications including phase insertion, removal, and renumbering.
/// </summary>
public interface IRoadmapModifier
{
    /// <summary>
    /// Inserts a new phase at the specified position with automatic renumbering.
    /// </summary>
    /// <param name="request">The modification request containing insertion details.</param>
    /// <param name="runId">The current run identifier for correlation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The modification result containing the new phase and updated roadmap.</returns>
    Task<RoadmapModifyResult> InsertPhaseAsync(RoadmapModifyRequest request, string runId, CancellationToken ct = default);

    /// <summary>
    /// Removes a phase by ID with safety checks for active phase protection.
    /// </summary>
    /// <param name="phaseId">The phase identifier to remove (e.g., PH-0001).</param>
    /// <param name="force">If true, allows removal of an active phase.</param>
    /// <param name="runId">The current run identifier for correlation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The modification result containing the updated roadmap or blocker information.</returns>
    Task<RoadmapModifyResult> RemovePhaseAsync(string phaseId, bool force, string runId, CancellationToken ct = default);

    /// <summary>
    /// Renumber all phases to ensure consistent PH-#### sequencing.
    /// </summary>
    /// <param name="runId">The current run identifier for correlation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The modification result containing the renumbered roadmap.</returns>
    Task<RoadmapModifyResult> RenumberPhasesAsync(string runId, CancellationToken ct = default);
}
