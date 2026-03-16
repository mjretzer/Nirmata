using nirmata.Agents.Execution.ControlPlane;

namespace nirmata.Agents.Execution.Validation;

/// <summary>
/// Validates handler outputs before they are persisted.
/// </summary>
public sealed class OutputValidator : IOutputValidator
{
    /// <inheritdoc />
    public Task<ValidationResult> ValidateAsync(OrchestratorResult result, CancellationToken ct = default)
    {
        // For now, this validator will always pass.
        // Future implementations can add checks for output schema, artifact integrity, etc.
        return Task.FromResult(ValidationResult.Success);
    }
}
