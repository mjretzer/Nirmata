namespace Gmsd.Agents.Execution.Planning.PhasePlanner.ContextGatherer;

/// <summary>
/// Defines the contract for gathering context about a phase to support task planning.
/// </summary>
public interface IPhaseContextGatherer
{
    /// <summary>
    /// Gathers comprehensive context for a phase to support planning.
    /// </summary>
    /// <param name="phaseId">The unique identifier of the phase to gather context for.</param>
    /// <param name="runId">The current run identifier for correlation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A phase brief containing gathered context, goals, constraints, and scope.</returns>
    Task<PhaseBrief> GatherContextAsync(string phaseId, string runId, CancellationToken ct = default);
}
