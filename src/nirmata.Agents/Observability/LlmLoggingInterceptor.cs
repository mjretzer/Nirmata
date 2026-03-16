namespace nirmata.Agents.Observability;

/// <summary>
/// Logging interceptor for LLM calls.
/// Records all LLM requests and responses for debugging and monitoring.
/// </summary>
public class LlmLoggingInterceptor : ILlmInterceptor
{
    private readonly ILogger<LlmLoggingInterceptor>? _logger;

    public string Name => "LLM Logging";
    public int Priority => 100;

    public LlmLoggingInterceptor(ILogger<LlmLoggingInterceptor>? logger = null)
    {
        _logger = logger;
    }

    public Task<bool> OnBeforeRequestAsync(LlmRequestContext context, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation(
            "LLM Request [RequestId: {RequestId}, Model: {Model}, CorrelationId: {CorrelationId}] - Prompt length: {PromptLength}",
            context.RequestId,
            context.Model,
            context.CorrelationId,
            context.Prompt.Length);

        return Task.FromResult(true);
    }

    public Task<bool> OnAfterResponseAsync(LlmResponseContext context, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation(
            "LLM Response [RequestId: {RequestId}, Model: {Model}] - Duration: {DurationMs}ms, Content length: {ContentLength}, Accepted: {IsAccepted}",
            context.RequestId,
            context.Model,
            context.DurationMs,
            context.Content.Length,
            context.IsAccepted);

        return Task.FromResult(context.IsAccepted);
    }

    public Task OnErrorAsync(LlmErrorContext context, CancellationToken cancellationToken = default)
    {
        _logger?.LogError(
            context.Exception,
            "LLM Error [RequestId: {RequestId}, Model: {Model}, ErrorCode: {ErrorCode}, Recoverable: {IsRecoverable}]",
            context.RequestId,
            context.Model,
            context.ErrorCode,
            context.IsRecoverable);

        return Task.CompletedTask;
    }
}
