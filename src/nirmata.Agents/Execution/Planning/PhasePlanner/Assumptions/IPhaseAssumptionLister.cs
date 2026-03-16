namespace nirmata.Agents.Execution.Planning.PhasePlanner.Assumptions;

/// <summary>
/// Defines the contract for extracting and documenting assumptions from phase planning.
/// </summary>
public interface IPhaseAssumptionLister
{
    /// <summary>
    /// Extracts assumptions from a phase brief and task plan.
    /// </summary>
    /// <param name="brief">The phase brief containing context.</param>
    /// <param name="taskPlan">The task plan containing decomposed tasks.</param>
    /// <param name="runId">The current run identifier for correlation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of assumptions identified during planning.</returns>
    Task<IReadOnlyList<PhaseAssumption>> ExtractAssumptionsAsync(
        PhaseBrief brief, 
        TaskPlan taskPlan, 
        string runId, 
        CancellationToken ct = default);

    /// <summary>
    /// Generates an assumptions document from the identified assumptions.
    /// </summary>
    /// <param name="assumptions">The list of assumptions to document.</param>
    /// <param name="runId">The current run identifier for correlation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The path to the generated assumptions markdown file.</returns>
    Task<string> GenerateAssumptionsDocumentAsync(
        IReadOnlyList<PhaseAssumption> assumptions, 
        string runId, 
        CancellationToken ct = default);
}
