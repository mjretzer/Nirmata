namespace nirmata.Agents.Execution.ControlPlane;

/// <summary>
/// Interface for the gating engine that evaluates workspace state and determines the appropriate workflow phase.
/// </summary>
public interface IGatingEngine
{
    /// <summary>
    /// Evaluates the workspace state and returns a gating result with the target phase.
    /// </summary>
    /// <param name="context">The gating context containing workspace state.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The gating result with target phase and reason.</returns>
    Task<GatingResult> EvaluateAsync(GatingContext context, CancellationToken ct = default);
}
