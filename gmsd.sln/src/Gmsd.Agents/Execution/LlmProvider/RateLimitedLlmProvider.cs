using Gmsd.Aos.Concurrency;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Gmsd.Agents.Execution.LlmProvider;

/// <summary>
/// Wraps IChatCompletionService with LLM call rate limiting.
/// </summary>
public sealed class RateLimitedLlmProvider : IChatCompletionService
{
    private readonly IChatCompletionService _innerService;
    private readonly IConcurrencyLimiter _concurrencyLimiter;
    private readonly ILogger<RateLimitedLlmProvider> _logger;

    public RateLimitedLlmProvider(
        IChatCompletionService innerService,
        IConcurrencyLimiter concurrencyLimiter,
        ILogger<RateLimitedLlmProvider> logger)
    {
        _innerService = innerService ?? throw new ArgumentNullException(nameof(innerService));
        _concurrencyLimiter = concurrencyLimiter ?? throw new ArgumentNullException(nameof(concurrencyLimiter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var acquired = await _concurrencyLimiter.TryAcquireLlmCallSlotAsync(cancellationToken);
        if (!acquired)
        {
            var metrics = _concurrencyLimiter.GetMetrics();
            var errorMessage = $"LLM call rate limit reached. Current active calls: {metrics.ActiveLlmCallCount}, " +
                             $"max parallel calls: {_concurrencyLimiter.GetMaxParallelLlmCalls()}. " +
                             $"Retry after 5 seconds.";
            
            _logger.LogWarning("LLM call rate limit exceeded: {ErrorMessage}", errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        try
        {
            await foreach (var content in _innerService.GetStreamingChatMessageContentsAsync(
                chatHistory,
                executionSettings,
                kernel,
                cancellationToken))
            {
                yield return content;
            }
        }
        finally
        {
            _concurrencyLimiter.ReleaseLlmCallSlot();
        }
    }

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var acquired = await _concurrencyLimiter.TryAcquireLlmCallSlotAsync(cancellationToken);
        if (!acquired)
        {
            var metrics = _concurrencyLimiter.GetMetrics();
            var errorMessage = $"LLM call rate limit reached. Current active calls: {metrics.ActiveLlmCallCount}, " +
                             $"max parallel calls: {_concurrencyLimiter.GetMaxParallelLlmCalls()}. " +
                             $"Retry after 5 seconds.";
            
            _logger.LogWarning("LLM call rate limit exceeded: {ErrorMessage}", errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        try
        {
            return await _innerService.GetChatMessageContentsAsync(
                chatHistory,
                executionSettings,
                kernel,
                cancellationToken);
        }
        finally
        {
            _concurrencyLimiter.ReleaseLlmCallSlot();
        }
    }

    public IReadOnlyDictionary<string, object?> Attributes => _innerService.Attributes;
}
