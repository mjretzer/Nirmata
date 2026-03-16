namespace nirmata.Agents.Observability;

/// <summary>
/// Defines the contract for distributed tracing across orchestration components.
/// Provides correlation ID and run ID tracking for request-level and operation-level tracing.
/// </summary>
public interface ITracingProvider
{
    /// <summary>
    /// Gets the current correlation ID for the request.
    /// </summary>
    string CorrelationId { get; }

    /// <summary>
    /// Gets the current run ID for write operations.
    /// </summary>
    string? RunId { get; }

    /// <summary>
    /// Starts a new trace context with a correlation ID.
    /// </summary>
    /// <param name="correlationId">Optional correlation ID; generates new one if not provided</param>
    /// <returns>A disposable trace context</returns>
    ITraceContext StartTrace(string? correlationId = null);

    /// <summary>
    /// Starts a new run trace with both correlation ID and run ID.
    /// </summary>
    /// <param name="runId">The run ID for the operation</param>
    /// <param name="correlationId">Optional correlation ID; generates new one if not provided</param>
    /// <returns>A disposable trace context</returns>
    ITraceContext StartRunTrace(string runId, string? correlationId = null);

    /// <summary>
    /// Creates a child span within the current trace context.
    /// </summary>
    /// <param name="spanName">Name of the span</param>
    /// <param name="attributes">Optional span attributes</param>
    /// <returns>A disposable span context</returns>
    ISpanContext CreateSpan(string spanName, Dictionary<string, object>? attributes = null);

    /// <summary>
    /// Records an event within the current trace context.
    /// </summary>
    /// <param name="eventName">Name of the event</param>
    /// <param name="attributes">Optional event attributes</param>
    void RecordEvent(string eventName, Dictionary<string, object>? attributes = null);

    /// <summary>
    /// Records an exception in the current trace context.
    /// </summary>
    /// <param name="exception">The exception to record</param>
    /// <param name="attributes">Optional exception attributes</param>
    void RecordException(Exception exception, Dictionary<string, object>? attributes = null);

    /// <summary>
    /// Sets a tag on the current span.
    /// </summary>
    /// <param name="key">Tag key</param>
    /// <param name="value">Tag value</param>
    void SetTag(string key, object value);

    /// <summary>
    /// Gets the current trace context.
    /// </summary>
    ITraceContext? GetCurrentContext();
}

/// <summary>
/// Represents a trace context for a request or operation.
/// </summary>
public interface ITraceContext : IDisposable
{
    /// <summary>
    /// The correlation ID for this trace.
    /// </summary>
    string CorrelationId { get; }

    /// <summary>
    /// The run ID for this trace (if applicable).
    /// </summary>
    string? RunId { get; }

    /// <summary>
    /// The start time of the trace.
    /// </summary>
    DateTimeOffset StartTime { get; }

    /// <summary>
    /// Gets the duration of the trace so far.
    /// </summary>
    TimeSpan Elapsed { get; }
}

/// <summary>
/// Represents a span context within a trace.
/// </summary>
public interface ISpanContext : IDisposable
{
    /// <summary>
    /// The name of the span.
    /// </summary>
    string SpanName { get; }

    /// <summary>
    /// The start time of the span.
    /// </summary>
    DateTimeOffset StartTime { get; }

    /// <summary>
    /// Gets the duration of the span so far.
    /// </summary>
    TimeSpan Elapsed { get; }

    /// <summary>
    /// Sets an attribute on the span.
    /// </summary>
    /// <param name="key">Attribute key</param>
    /// <param name="value">Attribute value</param>
    void SetAttribute(string key, object value);

    /// <summary>
    /// Records an event within the span.
    /// </summary>
    /// <param name="eventName">Event name</param>
    /// <param name="attributes">Optional event attributes</param>
    void RecordEvent(string eventName, Dictionary<string, object>? attributes = null);
}
