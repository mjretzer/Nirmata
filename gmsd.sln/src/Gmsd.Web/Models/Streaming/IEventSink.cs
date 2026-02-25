using System.Threading.Channels;

namespace Gmsd.Web.Models.Streaming;

/// <summary>
/// Defines the event emission abstraction for streaming dialogue events.
/// Implementations provide different strategies for emitting events to consumers.
/// </summary>
public interface IEventSink
{
    /// <summary>
    /// Emits a streaming event.
    /// </summary>
    /// <param name="event">The event to emit</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the event was emitted successfully, false otherwise</returns>
    ValueTask<bool> EmitAsync(StreamingEvent @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Emits a streaming event with a typed payload.
    /// </summary>
    /// <typeparam name="TPayload">The payload type</typeparam>
    /// <param name="type">The event type</param>
    /// <param name="payload">The event payload</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <param name="sequenceNumber">Optional sequence number</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the event was emitted successfully, false otherwise</returns>
    ValueTask<bool> EmitAsync<TPayload>(
        StreamingEventType type,
        TPayload payload,
        string? correlationId = null,
        long? sequenceNumber = null,
        CancellationToken cancellationToken = default) where TPayload : class;

    /// <summary>
    /// Attempts to emit an event synchronously without blocking.
    /// Returns immediately with false if the event cannot be emitted.
    /// </summary>
    /// <param name="event">The event to emit</param>
    /// <returns>True if the event was accepted for emission, false otherwise</returns>
    bool TryEmit(StreamingEvent @event);

    /// <summary>
    /// Attempts to emit an event with a typed payload synchronously.
    /// </summary>
    /// <typeparam name="TPayload">The payload type</typeparam>
    /// <param name="type">The event type</param>
    /// <param name="payload">The event payload</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <param name="sequenceNumber">Optional sequence number</param>
    /// <returns>True if the event was accepted for emission, false otherwise</returns>
    bool TryEmit<TPayload>(
        StreamingEventType type,
        TPayload payload,
        string? correlationId = null,
        long? sequenceNumber = null) where TPayload : class;

    /// <summary>
    /// Completes the event sink, indicating no more events will be emitted.
    /// </summary>
    /// <param name="exception">Optional exception if completion is due to an error</param>
    void Complete(Exception? exception = null);

    /// <summary>
    /// Gets a value indicating whether the sink has been completed.
    /// </summary>
    bool IsCompleted { get; }
}
