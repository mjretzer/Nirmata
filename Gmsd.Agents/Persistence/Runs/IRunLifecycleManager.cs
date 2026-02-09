using Gmsd.Agents.Execution.ControlPlane;
using Gmsd.Agents.Models.Runtime;

namespace Gmsd.Agents.Persistence.Runs;

/// <summary>
/// Interface for managing the run lifecycle including evidence folder creation and event recording.
/// </summary>
public interface IRunLifecycleManager
{
    /// <summary>
    /// Starts a new run, creating evidence folder and run context.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The run context with new RunId.</returns>
    Task<RunContext> StartRunAsync(CancellationToken ct = default);

    /// <summary>
    /// Records the input intent to the run.
    /// </summary>
    /// <param name="runId">The run identifier.</param>
    /// <param name="intent">The workflow intent.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AttachInputAsync(string runId, WorkflowIntent intent, CancellationToken ct = default);

    /// <summary>
    /// Finalizes the run with status and outputs.
    /// </summary>
    /// <param name="runId">The run identifier.</param>
    /// <param name="success">Whether the run succeeded.</param>
    /// <param name="outputs">Output data from the run.</param>
    /// <param name="ct">Cancellation token.</param>
    Task FinishRunAsync(string runId, bool success, Dictionary<string, object>? outputs, CancellationToken ct = default);

    /// <summary>
    /// Records a command dispatch to the run's command log.
    /// </summary>
    /// <param name="runId">The run identifier.</param>
    /// <param name="group">Command group.</param>
    /// <param name="command">Command name.</param>
    /// <param name="status">Command status.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RecordCommandAsync(string runId, string group, string command, string status, CancellationToken ct = default);
}
