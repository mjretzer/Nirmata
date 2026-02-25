using System.Threading.Channels;

namespace Gmsd.Web.Models.Streaming;

/// <summary>
/// Event sink implementation using System.Threading.Channels for async event streaming.
/// Provides backpressure handling and bounded/unbounded channel options.
/// </summary>
public class ChannelEventSink : IEventSink, IDisposable
{
    private readonly Channel<StreamingEvent> _channel;
    private readonly bool _completeOnWriterClose;
    private int _isCompleted;

    /// <summary>
    /// Creates an unbounded channel event sink.
    /// </summary>
    /// <param name="completeOnWriterClose">Whether to complete the sink when all writers are done</param>
    public ChannelEventSink(bool completeOnWriterClose = true)
    {
        _channel = Channel.CreateUnbounded<StreamingEvent>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });
        _completeOnWriterClose = completeOnWriterClose;
    }

    /// <summary>
    /// Creates a bounded channel event sink with specified capacity.
    /// </summary>
    /// <param name="capacity">Maximum number of events to hold in the channel</param>
    /// <param name="fullMode">Behavior when channel is full</param>
    public ChannelEventSink(int capacity, BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait)
    {
        _channel = Channel.CreateBounded<StreamingEvent>(new BoundedChannelOptions(capacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = fullMode
        });
        _completeOnWriterClose = true;
    }

    /// <summary>
    /// Creates a channel event sink with an existing channel.
    /// </summary>
    /// <param name="channel">The channel to use for event emission</param>
    /// <param name="completeOnWriterClose">Whether to complete the sink when all writers are done</param>
    public ChannelEventSink(Channel<StreamingEvent> channel, bool completeOnWriterClose = true)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _completeOnWriterClose = completeOnWriterClose;
    }

    /// <summary>
    /// Gets the channel reader for consuming events.
    /// </summary>
    public ChannelReader<StreamingEvent> Reader => _channel.Reader;

    /// <inheritdoc />
    public bool IsCompleted => Interlocked.CompareExchange(ref _isCompleted, 0, 0) == 1;

    /// <inheritdoc />
    public async ValueTask<bool> EmitAsync(StreamingEvent @event, CancellationToken cancellationToken = default)
    {
        if (IsCompleted)
            return false;

        if (@event == null)
            throw new ArgumentNullException(nameof(@event));

        try
        {
            await _channel.Writer.WriteAsync(@event, cancellationToken);
            return true;
        }
        catch (ChannelClosedException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public ValueTask<bool> EmitAsync<TPayload>(
        StreamingEventType type,
        TPayload payload,
        string? correlationId = null,
        long? sequenceNumber = null,
        CancellationToken cancellationToken = default) where TPayload : class
    {
        var @event = StreamingEvent.Create(type, payload, correlationId, sequenceNumber);
        return EmitAsync(@event, cancellationToken);
    }

    /// <inheritdoc />
    public bool TryEmit(StreamingEvent @event)
    {
        if (IsCompleted)
            return false;

        if (@event == null)
            throw new ArgumentNullException(nameof(@event));

        return _channel.Writer.TryWrite(@event);
    }

    /// <inheritdoc />
    public bool TryEmit<TPayload>(
        StreamingEventType type,
        TPayload payload,
        string? correlationId = null,
        long? sequenceNumber = null) where TPayload : class
    {
        var @event = StreamingEvent.Create(type, payload, correlationId, sequenceNumber);
        return TryEmit(@event);
    }

    /// <inheritdoc />
    public void Complete(Exception? exception = null)
    {
        if (Interlocked.CompareExchange(ref _isCompleted, 1, 0) == 0)
        {
            if (exception != null)
                _channel.Writer.Complete(exception);
            else
                _channel.Writer.Complete();
        }
    }

    /// <summary>
    /// Waits for the channel to complete reading all events.
    /// </summary>
    public Task WaitForCompletionAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.Completion.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Returns an async enumerable of all events from the channel.
    /// </summary>
    public IAsyncEnumerable<StreamingEvent> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Complete();
    }
}
