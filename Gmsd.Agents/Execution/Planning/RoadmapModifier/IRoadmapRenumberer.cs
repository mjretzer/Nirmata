namespace Gmsd.Agents.Execution.Planning.RoadmapModifier;

/// <summary>
/// Defines the contract for consistent phase ID sequencing logic.
/// </summary>
public interface IRoadmapRenumberer
{
    /// <summary>
    /// Generates a new phase ID in PH-#### format based on the next available sequence number.
    /// </summary>
    /// <param name="existingPhaseIds">Collection of existing phase IDs to determine the next sequence.</param>
    /// <returns>A new phase ID string in PH-#### format.</returns>
    string GenerateNextPhaseId(IEnumerable<string> existingPhaseIds);

    /// <summary>
    /// Renumber all phases sequentially starting from PH-0001.
    /// </summary>
    /// <param name="phases">The phases to renumber, ordered by their desired sequence.</param>
    /// <returns>A mapping of old phase IDs to new phase IDs.</returns>
    Dictionary<string, string> RenumberPhases(IEnumerable<PhaseReference> phases);

    /// <summary>
    /// Extracts the sequence number from a phase ID (e.g., PH-0001 returns 1).
    /// </summary>
    /// <param name="phaseId">The phase ID to parse.</param>
    /// <returns>The sequence number, or -1 if the format is invalid.</returns>
    int ExtractSequenceNumber(string phaseId);

    /// <summary>
    /// Validates that a phase ID is in the correct PH-#### format.
    /// </summary>
    /// <param name="phaseId">The phase ID to validate.</param>
    /// <returns>True if the format is valid; otherwise, false.</returns>
    bool IsValidPhaseIdFormat(string phaseId);
}

/// <summary>
/// Represents a phase reference for renumbering operations.
/// </summary>
public sealed class PhaseReference
{
    /// <summary>
    /// The current phase ID.
    /// </summary>
    public string PhaseId { get; set; } = string.Empty;

    /// <summary>
    /// The milestone ID this phase belongs to.
    /// </summary>
    public string MilestoneId { get; set; } = string.Empty;

    /// <summary>
    /// The desired sequence order for this phase.
    /// </summary>
    public int SequenceOrder { get; set; }

    /// <summary>
    /// The phase name for display purposes.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
