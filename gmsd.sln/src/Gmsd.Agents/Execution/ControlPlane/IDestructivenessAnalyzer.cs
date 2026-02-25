namespace Gmsd.Agents.Execution.ControlPlane;

/// <summary>
/// Analyzes operations to determine their risk level and destructiveness.
/// </summary>
public interface IDestructivenessAnalyzer
{
    /// <summary>
    /// Analyzes a phase and context to determine the risk level.
    /// </summary>
    /// <param name="phase">The target phase to analyze.</param>
    /// <param name="context">The gating context containing workspace state.</param>
    /// <returns>The risk level classification.</returns>
    RiskLevel AnalyzeRisk(string phase, GatingContext context);

    /// <summary>
    /// Analyzes a git operation to determine its risk level.
    /// </summary>
    /// <param name="operation">The git operation (e.g., "commit", "push", "merge").</param>
    /// <param name="arguments">The operation arguments.</param>
    /// <returns>The risk level classification for the git operation.</returns>
    RiskLevel AnalyzeGitOperationRisk(string operation, IReadOnlyList<string> arguments);

    /// <summary>
    /// Determines if an operation is a git mutating operation that requires confirmation.
    /// </summary>
    /// <param name="operation">The git operation to check.</param>
    /// <returns>True if the operation is a mutating git operation.</returns>
    bool IsGitMutatingOperation(string operation);

    /// <summary>
    /// Determines if an operation requires user confirmation based on risk and context.
    /// </summary>
    /// <param name="phase">The target phase.</param>
    /// <param name="context">The gating context.</param>
    /// <returns>True if confirmation is required.</returns>
    bool RequiresConfirmation(string phase, GatingContext context);

    /// <summary>
    /// Gets the side effects for a given phase.
    /// </summary>
    /// <param name="phase">The target phase.</param>
    /// <returns>List of side effect types.</returns>
    IReadOnlyList<string> GetSideEffects(string phase);
}
