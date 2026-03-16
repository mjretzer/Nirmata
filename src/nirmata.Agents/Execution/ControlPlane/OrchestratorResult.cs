namespace nirmata.Agents.Execution.ControlPlane;

/// <summary>
/// Represents the result of an orchestrator workflow execution.
/// </summary>
public sealed class OrchestratorResult
{
    /// <summary>
    /// Whether the orchestration completed successfully.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// The last phase that executed (e.g., "Executor", "Verifier").
    /// </summary>
    public string? FinalPhase { get; init; }

    /// <summary>
    /// Identifier for the run record created.
    /// </summary>
    public string? RunId { get; init; }

    /// <summary>
    /// Pointers to evidence artifacts produced.
    /// </summary>
    public Dictionary<string, object> Artifacts { get; init; } = new();
}
