namespace nirmata.Agents.Execution.Validation;

/// <summary>
/// Defines the contract for a pre-flight validator that checks conditions before an orchestrator run proceeds.
/// </summary>
public interface IPreflightValidator
{
    /// <summary>
    /// Validates the pre-flight conditions for a given workflow intent.
    /// </summary>
    /// <param name="intent">The workflow intent to validate.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating whether the validation passed.</returns>
    Task<ValidationResult> ValidateAsync(nirmata.Agents.Execution.ControlPlane.WorkflowIntent intent, CancellationToken ct = default);
}
