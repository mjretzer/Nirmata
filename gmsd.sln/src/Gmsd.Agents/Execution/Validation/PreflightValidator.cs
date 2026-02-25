using Gmsd.Agents.Execution.ControlPlane;

namespace Gmsd.Agents.Execution.Validation;

/// <summary>
/// Performs pre-flight validation checks before an orchestrator run proceeds.
/// </summary>
public sealed class PreflightValidator : IPreflightValidator
{
    /// <inheritdoc />
    public Task<ValidationResult> ValidateAsync(WorkflowIntent intent, CancellationToken ct = default)
    {
        // For now, this validator will always pass. 
        // Future implementations can add checks for schema versions, artifact integrity, etc.
        return Task.FromResult(ValidationResult.Success);
    }
}
