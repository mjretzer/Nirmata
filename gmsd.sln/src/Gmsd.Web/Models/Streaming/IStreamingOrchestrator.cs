using Gmsd.Agents.Execution.ControlPlane;

namespace Gmsd.Web.Models.Streaming;

/// <summary>
/// Interface for a streaming-capable orchestrator that emits typed dialogue events
/// during workflow execution. Implementations wrap the core orchestrator and expose
/// the agent's reasoning, decisions, and conversational turns as a stream of events.
/// </summary>
/// <remarks>
/// Usage pattern:
/// <code>
/// var orchestrator = serviceProvider.GetRequiredService&lt;IStreamingOrchestrator&gt;();
/// await foreach (var @event in orchestrator.ExecuteWithEventsAsync(intent, options, ct))
/// {
///     // Render event to UI, log, or forward via SSE
///     await RenderEventAsync(@event);
/// }
/// </code>
/// </remarks>
public interface IStreamingOrchestrator
{
    /// <summary>
    /// Executes a workflow intent through the orchestration pipeline and yields
    /// streaming events representing the agent's reasoning and dialogue.
    /// </summary>
    /// <param name="intent">The workflow intent containing input and correlation ID.</param>
    /// <param name="options">Configuration options for the streaming orchestration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async enumerable of streaming events including intent classification, gating decisions, phase transitions, tool calls, and assistant responses.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="intent"/> is null.</exception>
    /// <remarks>
    /// Events are yielded in approximate chronological order as they occur during execution.
    /// Consumers should handle event ordering via sequence numbers if strict ordering is required.
    /// </remarks>
    IAsyncEnumerable<StreamingEvent> ExecuteWithEventsAsync(
        WorkflowIntent intent,
        StreamingOrchestrationOptions? options = null,
        CancellationToken ct = default);
}
