#pragma warning disable CS0618 // Intentionally using obsolete ILlmProvider contracts pending migration

using System.Diagnostics;
using System.Runtime.CompilerServices;
using nirmata.Agents.Execution.ControlPlane.Chat.Models;
using nirmata.Agents.Execution.ControlPlane.Llm.Contracts;
using nirmata.Agents.Observability;
using Microsoft.Extensions.Logging;

namespace nirmata.Agents.Execution.ControlPlane.Chat;

/// <summary>
/// LLM-backed implementation of the chat responder.
/// </summary>
public sealed class LlmChatResponder : IChatResponder
{
    private readonly ILlmProvider _llmProvider;
    private readonly IChatContextAssembly _contextAssembly;
    private readonly ChatPromptBuilder _promptBuilder;
    private readonly ILogger<LlmChatResponder>? _logger;
    private readonly ICorrelationIdProvider? _correlationIdProvider;

    /// <summary>
    /// Default timeout for LLM calls.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Default temperature for chat responses.
    /// </summary>
    public float DefaultTemperature { get; init; } = 0.7f;

    /// <summary>
    /// Default max tokens for chat responses.
    /// </summary>
    public int DefaultMaxTokens { get; init; } = 1000;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmChatResponder"/> class.
    /// </summary>
    public LlmChatResponder(
        ILlmProvider llmProvider,
        IChatContextAssembly contextAssembly,
        ChatPromptBuilder promptBuilder,
        ILogger<LlmChatResponder>? logger = null,
        ICorrelationIdProvider? correlationIdProvider = null)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _contextAssembly = contextAssembly ?? throw new ArgumentNullException(nameof(contextAssembly));
        _promptBuilder = promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));
        _logger = logger;
        _correlationIdProvider = correlationIdProvider;
    }

    /// <inheritdoc />
    public async Task<ChatResponse> RespondAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = _correlationIdProvider?.Current;

        try
        {
            _logger?.LogInformation("Processing chat request with correlation {CorrelationId}", correlationId ?? "none");

            // Assemble context
            var context = await _contextAssembly.AssembleAsync(cancellationToken);

            if (!context.IsSuccess)
            {
                _logger?.LogWarning("Context assembly failed, using degraded mode");
            }

            // Build prompt
            var prompt = _promptBuilder.Build(request.Input, context);

            // Create LLM request
            var llmRequest = new LlmCompletionRequest
            {
                Messages = new List<LlmMessage>
                {
                    LlmMessage.System(prompt.System),
                    LlmMessage.User(prompt.User)
                },
                Options = new LlmProviderOptions
                {
                    Temperature = request.Temperature.HasValue ? (float)request.Temperature.Value : DefaultTemperature,
                    MaxTokens = request.MaxTokens ?? DefaultMaxTokens
                }
            };

            // Call LLM with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Timeout);

            var llmResponse = await _llmProvider.CompleteAsync(llmRequest, cts.Token);
            stopwatch.Stop();

            // Log slow responses
            if (stopwatch.Elapsed > TimeSpan.FromSeconds(3))
            {
                _logger?.LogWarning("Chat response took {DurationMs}ms (slower than 3s threshold)", stopwatch.ElapsedMilliseconds);
            }

            // Build response
            var response = new ChatResponse
            {
                Content = llmResponse.Message.Content ?? string.Empty,
                Model = llmResponse.Model,
                PromptTokens = llmResponse.Usage?.PromptTokens ?? 0,
                CompletionTokens = llmResponse.Usage?.CompletionTokens ?? 0,
                DurationMs = stopwatch.ElapsedMilliseconds,
                IsSuccess = true,
                CorrelationId = correlationId
            };

            _logger?.LogInformation(
                "Chat response generated in {DurationMs}ms, {TotalTokens} tokens",
                response.DurationMs,
                response.TotalTokens);

            return response;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout
            stopwatch.Stop();
            _logger?.LogError("Chat request timed out after {Timeout}s", Timeout.TotalSeconds);

            return new ChatResponse
            {
                Content = GetTimeoutFallbackMessage(),
                IsSuccess = false,
                ErrorMessage = "Request timed out",
                DurationMs = stopwatch.ElapsedMilliseconds,
                CorrelationId = correlationId
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "Failed to generate chat response");

            return new ChatResponse
            {
                Content = GetErrorFallbackMessage(),
                IsSuccess = false,
                ErrorMessage = ex.Message,
                DurationMs = stopwatch.ElapsedMilliseconds,
                CorrelationId = correlationId
            };
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatDelta> StreamResponseAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var correlationId = _correlationIdProvider?.Current;
        IAsyncEnumerable<LlmDelta>? stream = null;
        Exception? caughtException = null;

        try
        {
            _logger?.LogInformation("Processing streaming chat request with correlation {CorrelationId}", correlationId ?? "none");

            // Assemble context
            var context = await _contextAssembly.AssembleAsync(cancellationToken);

            // Build prompt
            var prompt = _promptBuilder.Build(request.Input, context);

            // Create LLM request
            var llmRequest = new LlmCompletionRequest
            {
                Messages = new List<LlmMessage>
                {
                    LlmMessage.System(prompt.System),
                    LlmMessage.User(prompt.User)
                },
                Options = new LlmProviderOptions
                {
                    Temperature = request.Temperature.HasValue ? (float)request.Temperature.Value : DefaultTemperature,
                    MaxTokens = request.MaxTokens ?? DefaultMaxTokens
                }
            };

            // Get the stream
            stream = _llmProvider.StreamCompletionAsync(llmRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize chat stream");
            caughtException = ex;
        }

        if (caughtException != null)
        {
            yield return new ChatDelta
            {
                Content = $"\n\n[Error: {caughtException.Message}]",
                IsComplete = true
            };
            yield break;
        }

        if (stream != null)
        {
            await foreach (var delta in stream.WithCancellation(cancellationToken))
            {
                yield return new ChatDelta
                {
                    Content = delta.Content,
                    IsComplete = false
                };
            }
        }

        // Signal completion
        yield return new ChatDelta
        {
            Content = null,
            IsComplete = true
        };
    }

    private static string GetTimeoutFallbackMessage()
    {
        return "I'm having trouble generating a response right now - the request timed out. " +
               "This might be due to high load or connectivity issues. Please try again in a moment.";
    }

    private static string GetErrorFallbackMessage()
    {
        return "I'm having trouble connecting to my language model right now. " +
               "Here are some things you can try:\n\n" +
               "- /help - Show available commands\n" +
               "- /status - Check workspace status\n\n" +
               "Please try again in a moment.";
    }
}

#pragma warning restore CS0618

