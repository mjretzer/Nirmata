using nirmata.Agents.Execution.ControlPlane.Streaming;

namespace nirmata.Agents.Tests.Fakes;

/// <summary>
/// Simple streaming event emitter used by HandlerTestHost.
/// Captures emitted events for assertions while keeping the surface minimal.
/// </summary>
public sealed class FakeStreamingEventEmitter : IStreamingEventEmitter
{
    private readonly List<ChatStreamingEvent> _chatEvents = new();
    private readonly List<ConfirmationEvent> _confirmationEvents = new();

    public IReadOnlyList<ChatStreamingEvent> ChatEvents => _chatEvents;

    public IReadOnlyList<ConfirmationEvent> ConfirmationEvents => _confirmationEvents;

    public string? CorrelationId { get; private set; }

    public void Emit(ChatStreamingEvent @event)
    {
        _chatEvents.Add(@event);
    }

    public void Emit(ConfirmationEvent @event)
    {
        _confirmationEvents.Add(@event);
    }

    public void SetCorrelationId(string correlationId)
    {
        CorrelationId = correlationId;
    }
}
