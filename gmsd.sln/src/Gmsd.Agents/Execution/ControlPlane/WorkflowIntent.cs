namespace Gmsd.Agents.Execution.ControlPlane;

/// <summary>
/// Represents a workflow intent with normalized input for orchestration.
/// </summary>
public sealed class WorkflowIntent
{
    /// <summary>
    /// Original user input (CLI args or freeform text).
    /// </summary>
    public required string InputRaw { get; init; }

    /// <summary>
    /// Classified/normalized command representation.
    /// </summary>
    public string? InputNormalized { get; init; }

    /// <summary>
    /// Tracing identifier.
    /// </summary>
    public required string CorrelationId { get; init; }
}
