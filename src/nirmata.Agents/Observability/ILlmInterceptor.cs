namespace nirmata.Agents.Observability;

/// <summary>
/// Defines the contract for LLM provider interceptors.
/// Allows filtering, logging, and monitoring of LLM calls at the provider boundary.
/// </summary>
public interface ILlmInterceptor
{
    /// <summary>
    /// Gets the name of this interceptor.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the priority of this interceptor (higher = executed first).
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Called before an LLM request is sent.
    /// </summary>
    /// <param name="context">The LLM request context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True to continue, false to block the request</returns>
    Task<bool> OnBeforeRequestAsync(LlmRequestContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after an LLM response is received.
    /// </summary>
    /// <param name="context">The LLM response context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True to continue, false to reject the response</returns>
    Task<bool> OnAfterResponseAsync(LlmResponseContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when an error occurs during LLM processing.
    /// </summary>
    /// <param name="context">The LLM error context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task OnErrorAsync(LlmErrorContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Context for an LLM request.
/// </summary>
public class LlmRequestContext
{
    /// <summary>
    /// Unique identifier for this request.
    /// </summary>
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The model being used.
    /// </summary>
    public required string Model { get; set; }

    /// <summary>
    /// The prompt or messages being sent.
    /// </summary>
    public required string Prompt { get; set; }

    /// <summary>
    /// Request parameters (temperature, max_tokens, etc.).
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Correlation ID for tracing.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Timestamp when the request was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Custom metadata for the request.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Context for an LLM response.
/// </summary>
public class LlmResponseContext
{
    /// <summary>
    /// The request ID that this response corresponds to.
    /// </summary>
    public required string RequestId { get; set; }

    /// <summary>
    /// The model that generated the response.
    /// </summary>
    public required string Model { get; set; }

    /// <summary>
    /// The response content.
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// Response metadata (tokens used, finish reason, etc.).
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Duration of the LLM call in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Timestamp when the response was received.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Whether the response should be accepted.
    /// </summary>
    public bool IsAccepted { get; set; } = true;

    /// <summary>
    /// Reason for rejection if IsAccepted is false.
    /// </summary>
    public string? RejectionReason { get; set; }
}

/// <summary>
/// Context for an LLM error.
/// </summary>
public class LlmErrorContext
{
    /// <summary>
    /// The request ID that caused the error.
    /// </summary>
    public required string RequestId { get; set; }

    /// <summary>
    /// The model that was being used.
    /// </summary>
    public required string Model { get; set; }

    /// <summary>
    /// The exception that occurred.
    /// </summary>
    public required Exception Exception { get; set; }

    /// <summary>
    /// Error code if available.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Whether the error is recoverable.
    /// </summary>
    public bool IsRecoverable { get; set; }

    /// <summary>
    /// Timestamp when the error occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Custom metadata for the error.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
