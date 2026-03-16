using nirmata.Agents.Execution.ControlPlane;

namespace nirmata.Agents.Execution.Validation;

/// <summary>
/// Defines the contract for a validator that checks handler outputs before they are persisted.
/// </summary>
public interface IOutputValidator
{
    /// <summary>
    /// Validates the output of a phase execution.
    /// </summary>
    /// <param name="result">The result from the orchestrator.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating whether the validation passed.</returns>
    Task<ValidationResult> ValidateAsync(OrchestratorResult result, CancellationToken ct = default);
}
