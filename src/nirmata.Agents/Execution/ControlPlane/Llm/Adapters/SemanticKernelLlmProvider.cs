#pragma warning disable CS0618 // Intentionally implementing obsolete ILlmProvider interface

using System.Runtime.CompilerServices;
using System.Text.Json;
using nirmata.Agents.Execution.ControlPlane.Llm.Contracts;
using nirmata.Agents.Observability;
using Json.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace nirmata.Agents.Execution.ControlPlane.Llm.Adapters;

/// <summary>
/// LLM provider adapter that delegates to Semantic Kernel's IChatCompletionService.
/// Translates between custom ILlmProvider contracts and SK's ChatCompletion abstractions.
/// </summary>
public sealed class SemanticKernelLlmProvider : ILlmProvider
{
    private readonly IChatCompletionService _chatCompletionService;
    private readonly ILogger<SemanticKernelLlmProvider> _logger;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private static readonly Random _random = new();
    private const int MaxRetries = 3;
    private const int BaseDelayMs = 100;
    private static readonly Dictionary<string, Json.Schema.JsonSchema> SchemaCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SemanticKernelLlmProvider"/> class.
    /// </summary>
    /// <param name="chatCompletionService">The SK chat completion service to delegate to.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="correlationIdProvider">The correlation ID provider.</param>
    public SemanticKernelLlmProvider(
        IChatCompletionService chatCompletionService,
        ILogger<SemanticKernelLlmProvider> logger,
        ICorrelationIdProvider correlationIdProvider)
    {
        _chatCompletionService = chatCompletionService ?? throw new ArgumentNullException(nameof(chatCompletionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _correlationIdProvider = correlationIdProvider ?? throw new ArgumentNullException(nameof(correlationIdProvider));
    }

    private static void EnforceStructuredOutputSchema(
        LlmStructuredOutputSchema schema,
        ChatMessageContent content,
        string providerName)
    {
        var raw = content.Content;
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new LlmProviderException(
                providerName,
                $"LLM returned empty content for structured schema '{schema.Name}'.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(raw);
        }
        catch (JsonException ex)
        {
            throw new LlmProviderException(
                providerName,
                $"LLM response for structured schema '{schema.Name}' is not valid JSON: {ex.Message}",
                innerException: ex);
        }

        using (document)
        {
            var compiled = GetCachedCompiledSchema(schema);
            var options = new EvaluationOptions
            {
                OutputFormat = OutputFormat.List
            };

            var result = compiled.Evaluate(document.RootElement, options);
            if (result.IsValid)
            {
                return;
            }

            var issues = CollectValidationErrors(result).ToList();
            var message = issues.Count > 0
                ? string.Join("; ", issues)
                : "Schema validation failed.";

            throw new LlmProviderException(
                providerName,
                $"LLM response failed schema '{schema.Name}' validation: {message}");
        }
    }

    private static Json.Schema.JsonSchema GetCachedCompiledSchema(LlmStructuredOutputSchema schema)
    {
        if (SchemaCache.TryGetValue(schema.Name, out var cached))
        {
            return cached;
        }

        var compiled = schema.GetCompiledSchema();
        SchemaCache[schema.Name] = compiled;
        return compiled;
    }

    private static IEnumerable<string> CollectValidationErrors(EvaluationResults results)
    {
        if (results.Errors is not null)
        {
            foreach (var error in results.Errors)
            {
                var location = string.IsNullOrEmpty(results.InstanceLocation.ToString())
                    ? "$"
                    : results.InstanceLocation.ToString();
                yield return $"{location}: {error.Value}";
            }
        }

        if (results.Details is null)
        {
            yield break;
        }

        foreach (var detail in results.Details)
        {
            foreach (var issue in CollectValidationErrors(detail))
            {
                yield return issue;
            }
        }
    }

    /// <inheritdoc />
    public string ProviderName => "semantic-kernel";

    /// <inheritdoc />
    public async Task<LlmCompletionResponse> CompleteAsync(
        LlmCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var chatHistory = ToChatHistory(request.Messages);
        var settings = ToPromptExecutionSettings(request);
        var correlationId = _correlationIdProvider.Current;
        var modelId = request.Model ?? "default";

        _logger.LogInformation(
            "Starting LLM completion request. Provider: {ProviderName}, Model: {ModelId}, CorrelationId: {CorrelationId}",
            ProviderName, modelId, correlationId);

        return await RetryWithExponentialBackoffAsync(
            async () =>
            {
                var result = await _chatCompletionService.GetChatMessageContentAsync(
                    chatHistory,
                    settings,
                    kernel: null,
                    cancellationToken).ConfigureAwait(false);

                if (request.StructuredOutputSchema is { StrictValidation: true } schema)
                {
                    EnforceStructuredOutputSchema(schema, result, ProviderName);
                }

                return ToLlmCompletionResponse(result, request);
            },
            async (response) =>
            {
                if (response.Usage is not null)
                {
                    _logger.LogInformation(
                        "LLM completion successful. Provider: {ProviderName}, Model: {ModelId}, CorrelationId: {CorrelationId}, PromptTokens: {PromptTokens}, CompletionTokens: {CompletionTokens}",
                        ProviderName, modelId, correlationId, response.Usage.PromptTokens, response.Usage.CompletionTokens);
                }
                else
                {
                    _logger.LogInformation(
                        "LLM completion successful. Provider: {ProviderName}, Model: {ModelId}, CorrelationId: {CorrelationId}",
                        ProviderName, modelId, correlationId);
                }
            },
            (ex) =>
            {
                var isRetryable = IsRetryable(ex);
                string? errorCode = (ex as HttpOperationException)?.StatusCode?.ToString();

                _logger.LogError(
                    ex,
                    "LLM completion failed. Provider: {ProviderName}, Model: {ModelId}, CorrelationId: {CorrelationId}, ErrorCode: {ErrorCode}, IsRetryable: {IsRetryable}, Message: {ErrorMessage}",
                    ProviderName, modelId, correlationId, errorCode, isRetryable, ex.Message);

                return new LlmProviderException(
                    ProviderName,
                    $"Failed to complete chat: {ex.Message}",
                    errorCode: errorCode,
                    isRetryable: isRetryable,
                    innerException: ex);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LlmDelta> StreamCompletionAsync(
        LlmCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.StructuredOutputSchema is not null)
        {
            throw new LlmProviderException(
                ProviderName,
                "Structured output schemas are not supported for streaming completions. Use CompleteAsync instead.");
        }

        var chatHistory = ToChatHistory(request.Messages);
        var settings = ToPromptExecutionSettings(request);
        var correlationId = _correlationIdProvider.Current;
        var modelId = request.Model ?? "default";

        _logger.LogInformation(
            "Starting LLM streaming request. Provider: {ProviderName}, Model: {ModelId}, CorrelationId: {CorrelationId}",
            ProviderName, modelId, correlationId);

        IAsyncEnumerable<StreamingChatMessageContent>? stream = null;
        Exception? lastException = null;

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                stream = _chatCompletionService.GetStreamingChatMessageContentsAsync(
                    chatHistory,
                    settings,
                    kernel: null,
                    cancellationToken);
                break;
            }
            catch (Exception ex)
            {
                lastException = ex;
                var isRetryable = IsRetryable(ex);

                if (!isRetryable || attempt >= MaxRetries)
                {
                    var errorCode = (ex as HttpOperationException)?.StatusCode?.ToString();
                    _logger.LogError(
                        ex,
                        "Failed to start LLM streaming request after {AttemptCount} attempts. Provider: {ProviderName}, Model: {ModelId}, CorrelationId: {CorrelationId}, ErrorCode: {ErrorCode}, IsRetryable: {IsRetryable}, Message: {ErrorMessage}",
                        attempt + 1, ProviderName, modelId, correlationId, errorCode, isRetryable, ex.Message);

                    throw new LlmProviderException(
                        ProviderName,
                        $"Failed to start streaming chat: {ex.Message}",
                        errorCode: errorCode,
                        isRetryable: isRetryable,
                        innerException: ex);
                }

                var delayMs = CalculateBackoffDelay(attempt);
                _logger.LogWarning(
                    "LLM streaming request failed on attempt {AttemptNumber}. Retrying after {DelayMs}ms. Provider: {ProviderName}, Model: {ModelId}, CorrelationId: {CorrelationId}",
                    attempt + 1, delayMs, ProviderName, modelId, correlationId);

                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
        }

        if (stream == null)
        {
            throw new LlmProviderException(
                ProviderName,
                $"Failed to start streaming chat: {lastException?.Message ?? "Unknown error"}",
                isRetryable: false,
                innerException: lastException);
        }

        await foreach (var chunk in stream.ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return ToLlmDelta(chunk);
        }
    }

    /// <summary>
    /// Converts a list of LLM messages to SK ChatHistory.
    /// </summary>
    private static ChatHistory ToChatHistory(IReadOnlyList<LlmMessage> messages)
    {
        var chatHistory = new ChatHistory();

        foreach (var message in messages)
        {
            var authorRole = ToAuthorRole(message.Role);

            if (message.Role == LlmMessageRole.Tool)
            {
                // Tool messages are added as function result content
                chatHistory.Add(new ChatMessageContent(
                    authorRole,
                    message.Content ?? string.Empty));
            }
            else if (message.ToolCalls?.Count > 0)
            {
                // Assistant message with tool calls - create with empty content first
                var chatMessage = new ChatMessageContent(authorRole, message.Content ?? string.Empty);
                
                // Add function call contents as items
                foreach (var tc in message.ToolCalls)
                {
                    chatMessage.Items.Add(new FunctionCallContent(
                        id: tc.Id,
                        functionName: tc.Name,
                        pluginName: string.Empty,
                        arguments: ParseArguments(tc.ArgumentsJson)));
                }

                chatHistory.Add(chatMessage);
            }
            else
            {
                // Regular message
                chatHistory.Add(new ChatMessageContent(
                    authorRole,
                    message.Content ?? string.Empty));
            }
        }

        return chatHistory;
    }

    /// <summary>
    /// Parses JSON arguments into a dictionary for SK FunctionCallContent.
    /// </summary>
    private static KernelArguments? ParseArguments(string? argumentsJson)
    {
        if (string.IsNullOrEmpty(argumentsJson))
            return null;

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(argumentsJson);
            if (dict == null)
                return null;

            return new KernelArguments(dict.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value));
        }
        catch
        {
            // If parsing fails, return null and let SK handle it
            return null;
        }
    }

    /// <summary>
    /// Converts LlmMessageRole to SK AuthorRole.
    /// </summary>
    private static AuthorRole ToAuthorRole(LlmMessageRole role) => role switch
    {
        LlmMessageRole.System => AuthorRole.System,
        LlmMessageRole.User => AuthorRole.User,
        LlmMessageRole.Assistant => AuthorRole.Assistant,
        LlmMessageRole.Tool => AuthorRole.Tool,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown message role")
    };

    /// <summary>
    /// Creates prompt execution settings from the request options.
    /// </summary>
    private static PromptExecutionSettings ToPromptExecutionSettings(LlmCompletionRequest request)
    {
        var settings = new PromptExecutionSettings();

        if (request.Model != null)
        {
            settings.ModelId = request.Model;
        }

        var options = request.Options;

        // Set OpenAI-compatible settings
        settings.ExtensionData ??= new Dictionary<string, object>();

        if (options.Temperature.HasValue)
            settings.ExtensionData["temperature"] = options.Temperature.Value;

        if (options.MaxTokens.HasValue)
            settings.ExtensionData["max_tokens"] = options.MaxTokens.Value;

        if (options.TopP.HasValue)
            settings.ExtensionData["top_p"] = options.TopP.Value;

        if (options.FrequencyPenalty.HasValue)
            settings.ExtensionData["frequency_penalty"] = options.FrequencyPenalty.Value;

        if (options.PresencePenalty.HasValue)
            settings.ExtensionData["presence_penalty"] = options.PresencePenalty.Value;

        if (options.StopSequences?.Count > 0)
            settings.ExtensionData["stop"] = options.StopSequences;

        if (options.Seed.HasValue)
            settings.ExtensionData["seed"] = options.Seed.Value;

        if (request.StructuredOutputSchema is not null)
        {
            settings.ExtensionData["response_format"] = request.StructuredOutputSchema.ToResponseFormatPayload();
        }
        else if (options.ResponseFormat != null)
        {
            settings.ExtensionData["response_format"] = options.ResponseFormat;
        }

        // Handle tool calling configuration
        if (request.Tools?.Count > 0)
        {
            settings.ExtensionData["tools"] = request.Tools.Select(t => new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = t.ParametersSchema
                }
            }).ToList();

            if (!string.IsNullOrEmpty(request.ToolChoice))
            {
                settings.ExtensionData["tool_choice"] = request.ToolChoice;
            }
        }

        return settings;
    }

    /// <summary>
    /// Converts SK ChatMessageContent to LlmCompletionResponse.
    /// </summary>
    private static LlmCompletionResponse ToLlmCompletionResponse(
        ChatMessageContent content,
        LlmCompletionRequest request)
    {
        var message = new LlmMessage
        {
            Role = ToLlmMessageRole(content.Role),
            Content = content.Content
        };

        // Extract tool calls from function call content items
        var toolCalls = content.Items
            .OfType<FunctionCallContent>()
            .Select(fc => new LlmToolCall
            {
                Id = fc.Id ?? Guid.NewGuid().ToString(),
                Name = fc.FunctionName,
                ArgumentsJson = SerializeArguments(fc.Arguments)
            })
            .ToList();

        if (toolCalls.Count > 0)
        {
            message = message with { ToolCalls = toolCalls };
        }

        // Extract token usage from metadata if available
        LlmTokenUsage? usage = null;
        if (content.Metadata?.TryGetValue("Usage", out var usageObj) == true &&
            usageObj is Dictionary<string, object?> usageDict)
        {
            if (usageDict.TryGetValue("InputTokens", out var inputTokens) &&
                usageDict.TryGetValue("OutputTokens", out var outputTokens))
            {
                usage = new LlmTokenUsage
                {
                    PromptTokens = Convert.ToInt32(inputTokens),
                    CompletionTokens = Convert.ToInt32(outputTokens)
                };
            }
        }

        // Extract finish reason
        string? finishReason = null;
        if (content.Metadata?.TryGetValue("FinishReason", out var finishReasonObj) == true)
        {
            finishReason = finishReasonObj?.ToString();
        }

        return new LlmCompletionResponse
        {
            Message = message,
            Model = content.ModelId ?? request.Model ?? "unknown",
            Provider = "semantic-kernel",
            Usage = usage,
            FinishReason = finishReason,
            ToolCalls = toolCalls.Count > 0 ? toolCalls : null,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Converts SK AuthorRole to LlmMessageRole.
    /// </summary>
    private static LlmMessageRole ToLlmMessageRole(AuthorRole role)
    {
        if (role == AuthorRole.System) return LlmMessageRole.System;
        if (role == AuthorRole.User) return LlmMessageRole.User;
        if (role == AuthorRole.Assistant) return LlmMessageRole.Assistant;
        if (role == AuthorRole.Tool) return LlmMessageRole.Tool;
        throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown author role");
    }

    /// <summary>
    /// Serializes function arguments to JSON string.
    /// </summary>
    private static string SerializeArguments(KernelArguments? arguments)
    {
        if (arguments == null || arguments.Count == 0)
            return "{}";

        var dict = arguments.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        return JsonSerializer.Serialize(dict);
    }

    /// <summary>
    /// Converts SK StreamingChatMessageContent to LlmDelta.
    /// </summary>
    private static LlmDelta ToLlmDelta(StreamingChatMessageContent chunk)
    {
        var content = chunk.Content ?? string.Empty;

        // Extract finish reason from metadata if present
        string? finishReason = null;
        if (chunk.Metadata?.TryGetValue("FinishReason", out var finishReasonObj) == true)
        {
            finishReason = finishReasonObj?.ToString();
        }

        // Extract usage from metadata if present (usually only on final chunk)
        LlmTokenUsage? usage = null;
        if (chunk.Metadata?.TryGetValue("Usage", out var usageObj) == true &&
            usageObj is Dictionary<string, object?> usageDict)
        {
            if (usageDict.TryGetValue("InputTokens", out var inputTokens) &&
                usageDict.TryGetValue("OutputTokens", out var outputTokens))
            {
                usage = new LlmTokenUsage
                {
                    PromptTokens = Convert.ToInt32(inputTokens),
                    CompletionTokens = Convert.ToInt32(outputTokens)
                };
            }
        }

        return new LlmDelta
        {
            Content = content,
            FinishReason = finishReason,
            Usage = usage
        };
    }

    /// <summary>
    /// Executes an operation with exponential backoff retry logic.
    /// </summary>
    private async Task<T> RetryWithExponentialBackoffAsync<T>(
        Func<Task<T>> operation,
        Func<T, Task> onSuccess,
        Func<Exception, LlmProviderException> createException,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var result = await operation().ConfigureAwait(false);
                await onSuccess(result).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex)
            {
                lastException = ex;
                var isRetryable = IsRetryable(ex);

                if (!isRetryable || attempt >= MaxRetries)
                {
                    throw createException(ex);
                }

                var delayMs = CalculateBackoffDelay(attempt);
                _logger.LogWarning(
                    "LLM operation failed on attempt {AttemptNumber}. Retrying after {DelayMs}ms. IsRetryable: {IsRetryable}",
                    attempt + 1, delayMs, isRetryable);

                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
        }

        throw createException(lastException ?? new InvalidOperationException("Unknown error"));
    }

    /// <summary>
    /// Calculates exponential backoff delay with jitter.
    /// Formula: BaseDelayMs * (2 ^ attempt) + random jitter (0 to 50% of calculated delay)
    /// </summary>
    private static int CalculateBackoffDelay(int attemptNumber)
    {
        var exponentialDelay = BaseDelayMs * (1 << attemptNumber); // 2^attemptNumber
        var jitter = _random.Next(0, exponentialDelay / 2);
        return exponentialDelay + jitter;
    }

    /// <summary>
    /// Determines if an exception is retryable.
    /// </summary>
    private static bool IsRetryable(Exception ex)
    {
        // Network errors and timeouts are typically retryable
        if (ex is HttpRequestException || ex is TimeoutException || ex is OperationCanceledException)
            return true;

        if (ex is HttpOperationException httpEx)
        {
            if (httpEx.StatusCode is System.Net.HttpStatusCode.TooManyRequests or System.Net.HttpStatusCode.ServiceUnavailable or System.Net.HttpStatusCode.GatewayTimeout or System.Net.HttpStatusCode.BadGateway or System.Net.HttpStatusCode.InternalServerError)
            {
                return true;
            }
        }

        // Rate limit errors (429) might be retryable but we'd need to check the response
        // For now, be conservative and don't retry other exceptions
        return false;
    }
}

#pragma warning restore CS0618
