using System.Threading.Channels;

namespace Gmsd.Web.Models.Streaming;

/// <summary>
/// Enhanced event sink with buffering, filtering, and sampling capabilities.
/// Provides production-ready event emission with performance optimization.
/// </summary>
public class EnhancedEventSink : IEventSink
{
    private readonly IEventSink _innerSink;
    private readonly Channel<StreamingEvent> _buffer;
    private readonly EventSinkOptions _options;
    private readonly Random _random;
    private long _eventCount;
    private long _filteredCount;

    public bool IsCompleted => _innerSink.IsCompleted;

    public EnhancedEventSink(IEventSink innerSink, EventSinkOptions? options = null)
    {
        _innerSink = innerSink ?? throw new ArgumentNullException(nameof(innerSink));
        _options = options ?? new EventSinkOptions();
        _buffer = Channel.CreateBounded<StreamingEvent>(
            new BoundedChannelOptions(_options.BufferSize) { FullMode = BoundedChannelFullMode.DropOldest });
        _random = new Random();
    }

    public async ValueTask<bool> EmitAsync(StreamingEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event == null)
            return false;

        // Apply sampling if configured
        if (_options.SamplingRate < 1.0 && _random.NextDouble() > _options.SamplingRate)
        {
            Interlocked.Increment(ref _filteredCount);
            return true; // Silently drop sampled events
        }

        // Apply filtering if configured
        if (_options.EventTypeFilter != null && _options.EventTypeFilter.Count > 0)
        {
            if (!_options.EventTypeFilter.Contains(@event.Type))
            {
                Interlocked.Increment(ref _filteredCount);
                return true; // Silently drop filtered events
            }
        }

        Interlocked.Increment(ref _eventCount);

        // Buffer the event if buffering is enabled
        if (_options.EnableBuffering)
        {
            await _buffer.Writer.WriteAsync(@event, cancellationToken);
        }

        // Emit to inner sink
        return await _innerSink.EmitAsync(@event, cancellationToken);
    }

    public async ValueTask<bool> EmitAsync<TPayload>(
        StreamingEventType type,
        TPayload payload,
        string? correlationId = null,
        long? sequenceNumber = null,
        CancellationToken cancellationToken = default) where TPayload : class
    {
        var @event = StreamingEvent.Create(type, payload, correlationId, sequenceNumber);
        return await EmitAsync(@event, cancellationToken);
    }

    public bool TryEmit(StreamingEvent @event)
    {
        if (@event == null)
            return false;

        // Apply sampling if configured
        if (_options.SamplingRate < 1.0 && _random.NextDouble() > _options.SamplingRate)
        {
            Interlocked.Increment(ref _filteredCount);
            return true;
        }

        // Apply filtering if configured
        if (_options.EventTypeFilter != null && _options.EventTypeFilter.Count > 0)
        {
            if (!_options.EventTypeFilter.Contains(@event.Type))
            {
                Interlocked.Increment(ref _filteredCount);
                return true;
            }
        }

        Interlocked.Increment(ref _eventCount);

        // Buffer the event if buffering is enabled
        if (_options.EnableBuffering)
        {
            _buffer.Writer.TryWrite(@event);
        }

        // Emit to inner sink
        return _innerSink.TryEmit(@event);
    }

    public bool TryEmit<TPayload>(
        StreamingEventType type,
        TPayload payload,
        string? correlationId = null,
        long? sequenceNumber = null) where TPayload : class
    {
        var @event = StreamingEvent.Create(type, payload, correlationId, sequenceNumber);
        return TryEmit(@event);
    }

    public void Complete(Exception? exception = null)
    {
        _buffer.Writer.Complete(exception);
        _innerSink.Complete(exception);
    }

    /// <summary>
    /// Gets statistics about event emission.
    /// </summary>
    public EventSinkStatistics GetStatistics()
    {
        return new EventSinkStatistics
        {
            TotalEventsEmitted = _eventCount,
            TotalEventsFiltered = _filteredCount,
            BufferedEventCount = _buffer.Reader.Count,
            IsCompleted = IsCompleted
        };
    }

    /// <summary>
    /// Flushes all buffered events.
    /// </summary>
    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        while (_buffer.Reader.TryRead(out var @event))
        {
            await _innerSink.EmitAsync(@event, cancellationToken);
        }
    }
}

/// <summary>
/// Options for enhanced event sink behavior.
/// </summary>
public class EventSinkOptions
{
    /// <summary>
    /// Whether to enable event buffering.
    /// </summary>
    public bool EnableBuffering { get; set; } = true;

    /// <summary>
    /// Maximum number of events to buffer.
    /// </summary>
    public int BufferSize { get; set; } = 1000;

    /// <summary>
    /// Sampling rate (0.0 to 1.0). 1.0 = all events, 0.5 = 50% of events.
    /// </summary>
    public double SamplingRate { get; set; } = 1.0;

    /// <summary>
    /// Event types to include. If empty, all types are included.
    /// </summary>
    public HashSet<StreamingEventType> EventTypeFilter { get; set; } = new();
}

/// <summary>
/// Statistics about event sink performance.
/// </summary>
public class EventSinkStatistics
{
    /// <summary>
    /// Total number of events emitted.
    /// </summary>
    public long TotalEventsEmitted { get; set; }

    /// <summary>
    /// Total number of events filtered/dropped.
    /// </summary>
    public long TotalEventsFiltered { get; set; }

    /// <summary>
    /// Current number of buffered events.
    /// </summary>
    public int BufferedEventCount { get; set; }

    /// <summary>
    /// Whether the sink has been completed.
    /// </summary>
    public bool IsCompleted { get; set; }
}
