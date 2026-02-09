namespace Gmsd.Agents.Execution.Brownfield.MapValidator;

/// <summary>
/// Defines the contract for the Map Validator.
/// Validates codebase map integrity, schema compliance, and cross-file invariants.
/// </summary>
public interface IMapValidator
{
    /// <summary>
    /// Validates a codebase map against all schemas and cross-file invariants.
    /// </summary>
    /// <param name="request">The validation request containing repository path and options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The validation result with any issues found.</returns>
    Task<MapValidationResult> ValidateAsync(MapValidationRequest request, CancellationToken ct = default);
}
