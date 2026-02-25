using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using Microsoft.Extensions.Logging;

#pragma warning disable CS0618
namespace Gmsd.Agents.Execution.ControlPlane.Llm;

/// <summary>
/// Handles retry logic for LLM completion requests with schema validation failures.
/// Implements exponential backoff with up to 3 retries for validation failures.
/// </summary>
public sealed class LlmRetryHandler
{
    private const int MaxRetries = 3;
    private const int InitialDelayMs = 1000;
    private const double BackoffMultiplier = 2.0;

    private readonly ILlmProvider _llmProvider;
    private readonly ILogger<LlmRetryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmRetryHandler"/> class.
    /// </summary>
    public LlmRetryHandler(ILlmProvider llmProvider, ILogger<LlmRetryHandler> logger)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes an LLM completion request with retry logic for schema validation failures.
    /// </summary>
    /// <param name="request">The completion request to execute.</param>
    /// <param name="schemaName">The name of the schema being validated.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The completion response on success.</returns>
    /// <exception cref="LlmProviderException">Thrown after max retries exhausted.</exception>
    public async Task<LlmCompletionResponse> ExecuteWithRetryAsync(
        LlmCompletionRequest request,
        string schemaName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(schemaName);

        LlmProviderException? lastException = null;

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var result = await _llmProvider.CompleteAsync(request, cancellationToken);
                
                if (attempt > 0)
                {
                    _logger.LogInformation(
                        "LLM completion succeeded on retry attempt {AttemptNumber} for schema '{SchemaName}'.",
                        attempt, schemaName);
                }

                return result;
            }
            catch (LlmProviderException ex) when (ex.Message.Contains("failed schema") && attempt < MaxRetries)
            {
                lastException = ex;
                var delayMs = (int)(InitialDelayMs * Math.Pow(BackoffMultiplier, attempt));

                _logger.LogWarning(
                    "LLM completion failed schema validation for '{SchemaName}' on attempt {AttemptNumber}. " +
                    "Retrying after {DelayMs}ms. Error: {ErrorMessage}",
                    schemaName, attempt + 1, delayMs, ex.Message);

                // Add clarification to the system prompt for retry
                var retryRequest = EnhanceRequestForRetry(request, attempt);
                request = retryRequest;

                await Task.Delay(delayMs, cancellationToken);
            }
            catch (LlmProviderException ex) when (attempt >= MaxRetries)
            {
                _logger.LogError(
                    ex,
                    "LLM completion failed schema validation for '{SchemaName}' after {MaxRetries} retries. " +
                    "Final error: {ErrorMessage}",
                    schemaName, MaxRetries, ex.Message);

                throw;
            }
            catch (LlmProviderException ex)
            {
                _logger.LogError(
                    ex,
                    "LLM completion failed with non-retryable error for schema '{SchemaName}': {ErrorMessage}",
                    schemaName, ex.Message);

                throw;
            }
        }

        // Should not reach here, but throw last exception if we do
        throw lastException ?? new LlmProviderException(
            "unknown",
            $"LLM completion failed for schema '{schemaName}' after {MaxRetries} retries.");
    }

    /// <summary>
    /// Enhances the request with additional clarification for retry attempts.
    /// </summary>
    private static LlmCompletionRequest EnhanceRequestForRetry(LlmCompletionRequest request, int attemptNumber)
    {
        var messages = request.Messages.ToList();
        
        // Find the system message and enhance it
        var systemMessageIndex = messages.FindIndex(m => m.Role == LlmMessageRole.System);
        if (systemMessageIndex >= 0)
        {
            var systemMessage = messages[systemMessageIndex];
            var enhancedContent = systemMessage.Content + 
                $"\n\n[Retry attempt {attemptNumber + 1}] " +
                "Ensure your response is VALID JSON that matches the provided schema exactly. " +
                "Check that all required fields are present and have the correct types.";
            
            messages[systemMessageIndex] = LlmMessage.System(enhancedContent);
        }

        return request with { Messages = messages };
    }
}
