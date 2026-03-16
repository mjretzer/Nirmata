using nirmata.Agents.Execution.ControlPlane;
using nirmata.Aos.Public;

namespace nirmata.Agents.Execution.Validation;

/// <summary>
/// Defines the contract for validating workspace prerequisites before workflow execution.
/// </summary>
public interface IPrerequisiteValidator
{
    /// <summary>
    /// Ensures baseline workspace state artifacts are initialized before write-phase dispatch.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A prerequisite result describing initialization success or failure.</returns>
    Task<PrerequisiteValidationResult> EnsureWorkspaceInitializedAsync(CancellationToken ct = default);

    /// <summary>
    /// Validates that all required prerequisites exist for the target phase.
    /// </summary>
    /// <param name="targetPhase">The target workflow phase.</param>
    /// <param name="context">The gating context containing workspace state.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result indicating whether prerequisites are satisfied and recovery options if not.</returns>
    Task<PrerequisiteValidationResult> ValidateAsync(string targetPhase, GatingContext context, CancellationToken ct = default);

    /// <summary>
    /// Checks the overall workspace initialization status.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result indicating workspace bootstrap status.</returns>
    Task<WorkspaceBootstrapResult> CheckWorkspaceBootstrapAsync(CancellationToken ct = default);
}
