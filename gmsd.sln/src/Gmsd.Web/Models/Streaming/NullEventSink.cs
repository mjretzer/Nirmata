namespace Gmsd.Web.Models.Streaming;

/// <summary>
/// No-op implementation of IEventSink that discards all events.
/// Useful for testing, scenarios where event streaming is disabled,
/// or as a default implementation when no sink is configured.
/// </summary>
public sealed class NullEventSink : IEventSink
{
    /// <summary>
    /// Singleton instance of the NullEventSink.
    /// </summary>
    public static readonly NullEventSink Instance = new();

    /// <inheritdoc />
    public bool IsCompleted => false;

    /// <inheritdoc />
    public ValueTask<bool> EmitAsync(StreamingEvent @event, CancellationToken cancellationToken = default)
    {
        // No-op: events are silently discarded
        return new ValueTask<bool>(true);
    }

    /// <inheritdoc />
    public ValueTask<bool> EmitAsync<TPayload>(
        StreamingEventType type,
        TPayload payload,
        string? correlationId = null,
        long? sequenceNumber = null,
        CancellationToken cancellationToken = default) where TPayload : class
    {
        // No-op: events are silently discarded
        return new ValueTask<bool>(true);
    }

    /// <inheritdoc />
    public bool TryEmit(StreamingEvent @event)
    {
        // No-op: always returns true, event is discarded
        return true;
    }

    /// <inheritdoc />
    public bool TryEmit<TPayload>(
        StreamingEventType type,
        TPayload payload,
        string? correlationId = null,
        long? sequenceNumber = null) where TPayload : class
    {
        // No-op: always returns true, event is discarded
        return true;
    }

    /// <inheritdoc />
    public void Complete(Exception? exception = null)
    {
        // No-op: nothing to complete
    }

    private NullEventSink()
    {
        // Private constructor to enforce singleton pattern
    }
}
