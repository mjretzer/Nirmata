namespace nirmata.Agents.Observability;

/// <summary>
/// Safety check interceptor for LLM calls.
/// Validates requests and responses against safety policies.
/// </summary>
public class LlmSafetyInterceptor : ILlmInterceptor
{
    private readonly ILogger<LlmSafetyInterceptor>? _logger;
    private readonly HashSet<string> _blockedPatterns;

    public string Name => "LLM Safety";
    public int Priority => 200;

    public LlmSafetyInterceptor(ILogger<LlmSafetyInterceptor>? logger = null)
    {
        _logger = logger;
        _blockedPatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "delete all",
            "drop database",
            "rm -rf",
            "format drive"
        };
    }

    public Task<bool> OnBeforeRequestAsync(LlmRequestContext context, CancellationToken cancellationToken = default)
    {
        // Check for blocked patterns in the prompt
        foreach (var pattern in _blockedPatterns)
        {
            if (context.Prompt.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogWarning(
                    "LLM Request [RequestId: {RequestId}] blocked due to safety policy - pattern detected: {Pattern}",
                    context.RequestId,
                    pattern);

                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }

    public Task<bool> OnAfterResponseAsync(LlmResponseContext context, CancellationToken cancellationToken = default)
    {
        // Check for blocked patterns in the response
        foreach (var pattern in _blockedPatterns)
        {
            if (context.Content.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogWarning(
                    "LLM Response [RequestId: {RequestId}] rejected due to safety policy - pattern detected: {Pattern}",
                    context.RequestId,
                    pattern);

                context.IsAccepted = false;
                context.RejectionReason = $"Response contains blocked pattern: {pattern}";
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }

    public Task OnErrorAsync(LlmErrorContext context, CancellationToken cancellationToken = default)
    {
        _logger?.LogWarning(
            "LLM Safety check encountered error [RequestId: {RequestId}]: {ErrorMessage}",
            context.RequestId,
            context.Exception.Message);

        return Task.CompletedTask;
    }
}
