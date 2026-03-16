using System.Collections.Concurrent;

namespace nirmata.Agents.Observability;

/// <summary>
/// Default implementation of the tracing provider using AsyncLocal for context propagation.
/// </summary>
public class TracingProvider : ITracingProvider
{
    private static readonly AsyncLocal<TraceContext?> CurrentTraceContext = new();
    private static readonly AsyncLocal<Stack<SpanContext>> SpanStack = new();

    public string CorrelationId => CurrentTraceContext.Value?.CorrelationId ?? string.Empty;
    public string? RunId => CurrentTraceContext.Value?.RunId;

    public ITraceContext StartTrace(string? correlationId = null)
    {
        var context = new TraceContext
        {
            CorrelationId = correlationId ?? Guid.NewGuid().ToString("N"),
            RunId = null,
            StartTime = DateTimeOffset.UtcNow
        };

        CurrentTraceContext.Value = context;
        SpanStack.Value ??= new Stack<SpanContext>();

        return context;
    }

    public ITraceContext StartRunTrace(string runId, string? correlationId = null)
    {
        var context = new TraceContext
        {
            CorrelationId = correlationId ?? Guid.NewGuid().ToString("N"),
            RunId = runId,
            StartTime = DateTimeOffset.UtcNow
        };

        CurrentTraceContext.Value = context;
        SpanStack.Value ??= new Stack<SpanContext>();

        return context;
    }

    public ISpanContext CreateSpan(string spanName, Dictionary<string, object>? attributes = null)
    {
        var span = new SpanContext
        {
            SpanName = spanName,
            StartTime = DateTimeOffset.UtcNow,
            Attributes = attributes ?? new Dictionary<string, object>()
        };

        SpanStack.Value ??= new Stack<SpanContext>();
        SpanStack.Value.Push(span);

        return span;
    }

    public void RecordEvent(string eventName, Dictionary<string, object>? attributes = null)
    {
        var span = GetCurrentSpan();
        if (span != null)
        {
            span.RecordEvent(eventName, attributes);
        }
    }

    public void RecordException(Exception exception, Dictionary<string, object>? attributes = null)
    {
        var span = GetCurrentSpan();
        if (span != null)
        {
            var attrs = attributes ?? new Dictionary<string, object>();
            attrs["exception.type"] = exception.GetType().FullName ?? "Unknown";
            attrs["exception.message"] = exception.Message;
            attrs["exception.stacktrace"] = exception.StackTrace ?? "N/A";

            span.RecordEvent("exception", attrs);
        }
    }

    public void SetTag(string key, object value)
    {
        var span = GetCurrentSpan();
        if (span != null)
        {
            span.SetAttribute(key, value);
        }
    }

    public ITraceContext? GetCurrentContext()
    {
        return CurrentTraceContext.Value;
    }

    private SpanContext? GetCurrentSpan()
    {
        var stack = SpanStack.Value;
        return stack?.Count > 0 ? stack.Peek() : null;
    }

    /// <summary>
    /// Internal trace context implementation.
    /// </summary>
    private class TraceContext : ITraceContext
    {
        public required string CorrelationId { get; init; }
        public string? RunId { get; init; }
        public required DateTimeOffset StartTime { get; init; }

        public TimeSpan Elapsed => DateTimeOffset.UtcNow - StartTime;

        public void Dispose()
        {
            // Clean up span stack
            var stack = SpanStack.Value;
            if (stack != null)
            {
                while (stack.Count > 0)
                {
                    stack.Pop().Dispose();
                }
            }

            CurrentTraceContext.Value = null;
        }
    }

    /// <summary>
    /// Internal span context implementation.
    /// </summary>
    private class SpanContext : ISpanContext
    {
        private readonly ConcurrentBag<(string EventName, Dictionary<string, object> Attributes)> _events = new();

        public required string SpanName { get; init; }
        public required DateTimeOffset StartTime { get; init; }
        public Dictionary<string, object> Attributes { get; init; } = new();

        public TimeSpan Elapsed => DateTimeOffset.UtcNow - StartTime;

        public void SetAttribute(string key, object value)
        {
            Attributes[key] = value;
        }

        public void RecordEvent(string eventName, Dictionary<string, object>? attributes = null)
        {
            _events.Add((eventName, attributes ?? new Dictionary<string, object>()));
        }

        public void Dispose()
        {
            // Pop from span stack
            var stack = SpanStack.Value;
            if (stack?.Count > 0)
            {
                stack.Pop();
            }
        }
    }
}
