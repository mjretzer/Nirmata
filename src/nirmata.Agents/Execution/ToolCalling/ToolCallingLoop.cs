using System.Text.Json;
using nirmata.Agents.Execution.ControlPlane.Tools.Registry;
using nirmata.Aos.Contracts.Tools;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace nirmata.Agents.Execution.ToolCalling;

/// <summary>
/// Implementation of the multi-step tool calling conversation protocol.
/// Manages the iterative cycle of sending tools to the LLM, executing tool calls,
/// and returning results until the conversation completes naturally or hits limits.
/// </summary>
public sealed class ToolCallingLoop : IToolCallingLoop
{
    private readonly IChatCompletionService _chatCompletionService;
    private readonly IToolRegistry _toolRegistry;
    private readonly IToolCallingEventEmitter? _eventEmitter;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolCallingLoop"/> class.
    /// </summary>
    /// <param name="chatCompletionService">The Semantic Kernel chat completion service.</param>
    /// <param name="toolRegistry">The tool registry for resolving and executing tools.</param>
    /// <param name="eventEmitter">Optional event emitter for observability.</param>
    public ToolCallingLoop(
        IChatCompletionService chatCompletionService,
        IToolRegistry toolRegistry,
        IToolCallingEventEmitter? eventEmitter = null)
    {
        _chatCompletionService = chatCompletionService ?? throw new ArgumentNullException(nameof(chatCompletionService));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _eventEmitter = eventEmitter;
    }

    /// <inheritdoc />
    public async Task<ToolCallingResult> ExecuteAsync(
        ToolCallingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var correlationId = request.CorrelationId ?? Guid.NewGuid().ToString("N");
        var startedAt = DateTime.UtcNow;
        var timeoutCts = new CancellationTokenSource(request.Options.Timeout);
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        // Initialize conversation state (declared outside try for catch block access)
        var conversationHistory = request.Messages.ToList();
        var totalPromptTokens = 0;
        var totalCompletionTokens = 0;
        var totalToolCalls = 0;
        var iteration = 0;

        try
        {
            // Remove the duplicate declarations from inside try block

            while (iteration < request.Options.MaxIterations)
            {
                iteration++;
                var iterationStartedAt = DateTime.UtcNow;

                // Check for cancellation
                linkedCts.Token.ThrowIfCancellationRequested();

                // Check token budget if configured
                if (request.Options.MaxTotalTokens.HasValue &&
                    (totalPromptTokens + totalCompletionTokens) >= request.Options.MaxTotalTokens.Value)
                {
                    return CreateResult(
                        conversationHistory,
                        iteration,
                        totalPromptTokens,
                        totalCompletionTokens,
                        ToolCallingCompletionReason.MaxIterationsReached, // Token limit treated as iteration limit
                        startedAt,
                        new ToolCallingError
                        {
                            Code = "TokenBudgetExceeded",
                            Message = $"Token budget exceeded: {totalPromptTokens + totalCompletionTokens} >= {request.Options.MaxTotalTokens}"
                        });
                }

                // Check tool call budget if configured
                if (totalToolCalls >= request.Options.MaxToolCalls)
                {
                    return CreateResult(
                        conversationHistory,
                        iteration,
                        totalPromptTokens,
                        totalCompletionTokens,
                        ToolCallingCompletionReason.MaxIterationsReached, // Tool limit treated as iteration limit or separate reason
                        startedAt,
                        new ToolCallingError
                        {
                            Code = "ToolCallBudgetExceeded",
                            Message = $"Tool call budget exceeded: {totalToolCalls} >= {request.Options.MaxToolCalls}"
                        });
                }

                // Step 1: Send conversation to LLM with available tools
                var chatHistory = ToChatHistory(conversationHistory);
                var settings = ToPromptExecutionSettings(request.Options, request.Tools);

                ChatMessageContent response;
                try
                {
                    response = await _chatCompletionService.GetChatMessageContentAsync(
                        chatHistory,
                        settings,
                        kernel: null,
                        linkedCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                {
                    return CreateResult(
                        conversationHistory,
                        iteration,
                        totalPromptTokens,
                        totalCompletionTokens,
                        ToolCallingCompletionReason.Timeout,
                        startedAt,
                        new ToolCallingError
                        {
                            Code = "Timeout",
                            Message = $"Tool calling loop exceeded timeout of {request.Options.Timeout}"
                        });
                }
                catch (Exception ex)
                {
                    EmitEvent(new ToolLoopFailedEvent
                    {
                        CorrelationId = correlationId,
                        Iteration = iteration,
                        ErrorCode = "LlmProviderError",
                        ErrorMessage = ex.Message
                    });

                    return CreateResult(
                        conversationHistory,
                        iteration,
                        totalPromptTokens,
                        totalCompletionTokens,
                        ToolCallingCompletionReason.Error,
                        startedAt,
                        new ToolCallingError
                        {
                            Code = "LlmProviderError",
                            Message = ex.Message,
                            ExceptionDetails = ex.ToString()
                        });
                }

                // Extract token usage from metadata if available
                var (promptTokens, completionTokens) = ExtractTokenUsage(response.Metadata);
                totalPromptTokens += promptTokens;
                totalCompletionTokens += completionTokens;

                // Add assistant response to conversation history
                var assistantMessage = ToToolCallingMessage(response);
                conversationHistory.Add(assistantMessage);

                // Step 2: Check if LLM requested tool calls
                var toolCalls = response.Items.OfType<FunctionCallContent>().ToList();
                if (toolCalls.Count == 0)
                {
                    // No tool calls - conversation completed naturally
                    EmitEvent(new ToolLoopIterationCompletedEvent
                    {
                        CorrelationId = correlationId,
                        Iteration = iteration,
                        HasMoreToolCalls = false,
                        ToolCallCount = 0,
                        Duration = DateTime.UtcNow - iterationStartedAt
                    });

                    EmitEvent(new ToolLoopCompletedEvent
                    {
                        CorrelationId = correlationId,
                        TotalIterations = iteration,
                        TotalToolCalls = totalToolCalls,
                        CompletionReason = ToolCallingCompletionReason.CompletedNaturally,
                        TotalDuration = DateTime.UtcNow - startedAt
                    });

                    return CreateResult(
                        conversationHistory,
                        iteration,
                        totalPromptTokens,
                        totalCompletionTokens,
                        ToolCallingCompletionReason.CompletedNaturally,
                        startedAt);
                }

                // Emit tool call detected event
                totalToolCalls += toolCalls.Count;
                EmitEvent(new ToolCallDetectedEvent
                {
                    CorrelationId = correlationId,
                    Iteration = iteration,
                    ToolCalls = toolCalls.Select(tc => new ToolCallDetectedInfo
                    {
                        ToolCallId = tc.Id ?? Guid.NewGuid().ToString("N"),
                        ToolName = tc.FunctionName,
                        ArgumentsJson = SerializeArguments(tc.Arguments)
                    }).ToList()
                });

                // Step 3 & 4: Execute tool calls and collect results
                var toolResults = await ExecuteToolCallsAsync(
                    toolCalls,
                    iteration,
                    correlationId,
                    request.Options,
                    request.Context,
                    linkedCts.Token).ConfigureAwait(false);

                // Step 5: Send tool results back to LLM
                foreach (var result in toolResults)
                {
                    var toolMessage = ToolCallingMessage.Tool(
                        result.ToolCallId,
                        result.ToolName,
                        result.IsSuccess
                            ? (result.ResultContent ?? "{}")
                            : JsonSerializer.Serialize(new { error = result.Error?.Message ?? "Unknown error" }));

                    conversationHistory.Add(toolMessage);
                }

                EmitEvent(new ToolResultsSubmittedEvent
                {
                    CorrelationId = correlationId,
                    Iteration = iteration,
                    ResultCount = toolResults.Count,
                    Results = toolResults.Select(r => new ToolResultSubmittedInfo
                    {
                        ToolCallId = r.ToolCallId,
                        ToolName = r.ToolName,
                        IsSuccess = r.IsSuccess
                    }).ToList()
                });

                EmitEvent(new ToolLoopIterationCompletedEvent
                {
                    CorrelationId = correlationId,
                    Iteration = iteration,
                    HasMoreToolCalls = true,
                    ToolCallCount = toolCalls.Count,
                    Duration = DateTime.UtcNow - iterationStartedAt
                });
            }

            // Max iterations reached
            EmitEvent(new ToolLoopCompletedEvent
            {
                CorrelationId = correlationId,
                TotalIterations = iteration,
                TotalToolCalls = totalToolCalls,
                CompletionReason = ToolCallingCompletionReason.MaxIterationsReached,
                TotalDuration = DateTime.UtcNow - startedAt
            });

            return CreateResult(
                conversationHistory,
                iteration,
                totalPromptTokens,
                totalCompletionTokens,
                ToolCallingCompletionReason.MaxIterationsReached,
                startedAt,
                new ToolCallingError
                {
                    Code = "MaxIterationsReached",
                    Message = $"Maximum iterations ({request.Options.MaxIterations}) reached"
                });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            EmitEvent(new ToolLoopFailedEvent
            {
                CorrelationId = correlationId,
                Iteration = iteration,
                ErrorCode = "Cancelled",
                ErrorMessage = "Tool calling loop was cancelled"
            });

            return CreateResult(
                conversationHistory,
                iteration,
                totalPromptTokens,
                totalCompletionTokens,
                ToolCallingCompletionReason.Cancelled,
                startedAt,
                new ToolCallingError
                {
                    Code = "Cancelled",
                    Message = "Tool calling loop was cancelled"
                });
        }
        finally
        {
            linkedCts.Dispose();
            timeoutCts.Dispose();
        }
    }

    /// <summary>
    /// Executes a list of tool calls, optionally in parallel.
    /// </summary>
    private async Task<List<ToolCallExecutionResult>> ExecuteToolCallsAsync(
        List<FunctionCallContent> toolCalls,
        int iteration,
        string correlationId,
        ToolCallingOptions options,
        IReadOnlyDictionary<string, object?> context,
        CancellationToken cancellationToken)
    {
        var results = new List<ToolCallExecutionResult>();

        if (options.EnableParallelToolExecution && toolCalls.Count > 1)
        {
            // Execute tool calls in parallel with throttling
            using var semaphore = new SemaphoreSlim(options.MaxParallelToolExecutions, options.MaxParallelToolExecutions);
            var tasks = toolCalls.Select(tc => ExecuteSingleToolCallAsync(tc, iteration, correlationId, context, semaphore, cancellationToken));
            var executedResults = await Task.WhenAll(tasks).ConfigureAwait(false);
            results.AddRange(executedResults);
        }
        else
        {
            // Execute tool calls sequentially
            foreach (var toolCall in toolCalls)
            {
                var result = await ExecuteSingleToolCallAsync(toolCall, iteration, correlationId, context, null, cancellationToken).ConfigureAwait(false);
                results.Add(result);
            }
        }

        return results;
    }

    /// <summary>
    /// Executes a single tool call.
    /// </summary>
    private async Task<ToolCallExecutionResult> ExecuteSingleToolCallAsync(
        FunctionCallContent toolCall,
        int iteration,
        string correlationId,
        IReadOnlyDictionary<string, object?> context,
        SemaphoreSlim? semaphore,
        CancellationToken cancellationToken)
    {
        var toolCallId = toolCall.Id ?? Guid.NewGuid().ToString("N");
        var toolName = toolCall.FunctionName;
        var argumentsJson = SerializeArguments(toolCall.Arguments);
        var startedAt = DateTime.UtcNow;

        // Emit started event
        EmitEvent(new ToolCallStartedEvent
        {
            CorrelationId = correlationId,
            Iteration = iteration,
            ToolCallId = toolCallId,
            ToolName = toolName,
            ArgumentsJson = argumentsJson
        });

        if (semaphore != null)
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            // Resolve tool from registry
            var tool = _toolRegistry.ResolveByName(toolName);
            if (tool == null)
            {
                var errorResult = ToolCallExecutionResult.Failure(
                    toolCallId,
                    toolName,
                    argumentsJson,
                    "ToolNotFound",
                    $"Tool '{toolName}' not found in registry",
                    startedAt);

                EmitEvent(new ToolCallFailedEvent
                {
                    CorrelationId = correlationId,
                    Iteration = iteration,
                    ToolCallId = toolCallId,
                    ToolName = toolName,
                    ErrorCode = "ToolNotFound",
                    ErrorMessage = $"Tool '{toolName}' not found in registry",
                    Duration = DateTime.UtcNow - startedAt
                });

                return errorResult;
            }

            // Parse arguments
            var arguments = ParseArguments(toolCall.Arguments);

            // Create tool request
            var toolRequest = new ToolRequest
            {
                Operation = toolName,
                Parameters = arguments ?? new Dictionary<string, object?>(),
                RequestId = toolCallId,
                Metadata = context.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? string.Empty)
            };

            // Create tool context
            var toolContext = new ToolContext
            {
                RunId = correlationId,
                CorrelationId = correlationId
            };

            // Execute tool
            var toolResult = await tool.InvokeAsync(toolRequest, toolContext, cancellationToken).ConfigureAwait(false);

            if (toolResult.IsSuccess)
            {
                var resultContent = toolResult.Data != null
                    ? JsonSerializer.Serialize(toolResult.Data)
                    : "{}";

                EmitEvent(new ToolCallCompletedEvent
                {
                    CorrelationId = correlationId,
                    Iteration = iteration,
                    ToolCallId = toolCallId,
                    ToolName = toolName,
                    Duration = DateTime.UtcNow - startedAt,
                    HasResult = true,
                    ResultSummary = resultContent.Length > 200
                        ? resultContent[..200] + "..."
                        : resultContent
                });

                return ToolCallExecutionResult.Success(
                    toolCallId,
                    toolName,
                    argumentsJson,
                    resultContent,
                    startedAt,
                    toolResult.Metadata);
            }
            else
            {
                var error = toolResult.Error!;

                EmitEvent(new ToolCallFailedEvent
                {
                    CorrelationId = correlationId,
                    Iteration = iteration,
                    ToolCallId = toolCallId,
                    ToolName = toolName,
                    ErrorCode = error.Code,
                    ErrorMessage = error.Message,
                    Duration = DateTime.UtcNow - startedAt
                });

                return ToolCallExecutionResult.Failure(
                    toolCallId,
                    toolName,
                    argumentsJson,
                    error.Code,
                    error.Message,
                    startedAt);
            }
        }
        catch (Exception ex)
        {
            EmitEvent(new ToolCallFailedEvent
            {
                CorrelationId = correlationId,
                Iteration = iteration,
                ToolCallId = toolCallId,
                ToolName = toolName,
                ErrorCode = "ExecutionError",
                ErrorMessage = ex.Message,
                Duration = DateTime.UtcNow - startedAt
            });

            return ToolCallExecutionResult.Failure(
                toolCallId,
                toolName,
                argumentsJson,
                "ExecutionError",
                ex.Message,
                startedAt,
                ex.ToString());
        }
        finally
        {
            semaphore?.Release();
        }
    }

    /// <summary>
    /// Emits an event if an event emitter is configured.
    /// </summary>
    private void EmitEvent(ToolCallingEvent @event)
    {
        _eventEmitter?.Emit(@event);
    }

    /// <summary>
    /// Creates the final result object.
    /// </summary>
    private static ToolCallingResult CreateResult(
        List<ToolCallingMessage> conversationHistory,
        int iterationCount,
        int totalPromptTokens,
        int totalCompletionTokens,
        ToolCallingCompletionReason completionReason,
        DateTime startedAt,
        ToolCallingError? error = null)
    {
        var finalMessage = conversationHistory.LastOrDefault() ??
            ToolCallingMessage.Assistant("No response generated");

        return new ToolCallingResult
        {
            FinalMessage = finalMessage,
            ConversationHistory = conversationHistory,
            IterationCount = iterationCount,
            CompletionReason = completionReason,
            Error = error,
            Usage = new ToolCallingUsageStats
            {
                TotalPromptTokens = totalPromptTokens,
                TotalCompletionTokens = totalCompletionTokens,
                IterationCount = iterationCount
            },
            Metadata = new Dictionary<string, string>
            {
                ["DurationMs"] = ((DateTime.UtcNow - startedAt).TotalMilliseconds).ToString("F0")
            }
        };
    }

    /// <summary>
    /// Converts tool calling messages to Semantic Kernel ChatHistory.
    /// </summary>
    private static ChatHistory ToChatHistory(IReadOnlyList<ToolCallingMessage> messages)
    {
        var chatHistory = new ChatHistory();

        foreach (var message in messages)
        {
            var authorRole = ToAuthorRole(message.Role);

            if (message.Role == ToolCallingRole.Tool)
            {
                // Tool messages need to reference the tool call they are responding to
                var chatMessage = new ChatMessageContent(
                    authorRole,
                    message.Content ?? string.Empty);

                // If we have tool call ID, we could store it in metadata or use SK's FunctionResultContent
                // For now, just add the content
                chatHistory.Add(chatMessage);
            }
            else if (message.ToolCalls?.Count > 0)
            {
                // Assistant message with tool calls
                var chatMessage = new ChatMessageContent(authorRole, message.Content ?? string.Empty);

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
    /// Converts a ToolCallingMessage to a ChatMessageContent.
    /// </summary>
    private static ToolCallingMessage ToToolCallingMessage(ChatMessageContent content)
    {
        var role = ToToolCallingRole(content.Role);

        // Extract tool calls from function call content items
        var toolCalls = content.Items
            .OfType<FunctionCallContent>()
            .Select(fc => new ToolCallingRequestItem
            {
                Id = fc.Id ?? Guid.NewGuid().ToString("N"),
                Name = fc.FunctionName,
                ArgumentsJson = SerializeArguments(fc.Arguments)
            })
            .ToList();

        return new ToolCallingMessage
        {
            Role = role,
            Content = content.Content,
            ToolCalls = toolCalls.Count > 0 ? toolCalls : null
        };
    }

    /// <summary>
    /// Converts ToolCallingRole to SK AuthorRole.
    /// </summary>
    private static AuthorRole ToAuthorRole(ToolCallingRole role) => role switch
    {
        ToolCallingRole.System => AuthorRole.System,
        ToolCallingRole.User => AuthorRole.User,
        ToolCallingRole.Assistant => AuthorRole.Assistant,
        ToolCallingRole.Tool => AuthorRole.Tool,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown message role")
    };

    /// <summary>
    /// Converts SK AuthorRole to ToolCallingRole.
    /// </summary>
    private static ToolCallingRole ToToolCallingRole(AuthorRole role)
    {
        if (role == AuthorRole.System) return ToolCallingRole.System;
        if (role == AuthorRole.User) return ToolCallingRole.User;
        if (role == AuthorRole.Assistant) return ToolCallingRole.Assistant;
        if (role == AuthorRole.Tool) return ToolCallingRole.Tool;
        throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown author role");
    }

    /// <summary>
    /// Parses JSON arguments into KernelArguments.
    /// </summary>
    private static KernelArguments? ParseArguments(string? argumentsJson)
    {
        if (string.IsNullOrEmpty(argumentsJson))
            return null;

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(argumentsJson);
            if (dict == null || dict.Count == 0)
                return null;

            return new KernelArguments(dict.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses KernelArguments into a dictionary.
    /// </summary>
    private static Dictionary<string, object?>? ParseArguments(KernelArguments? arguments)
    {
        if (arguments == null || arguments.Count == 0)
            return null;

        return arguments.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
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
    /// Creates prompt execution settings from tool calling options.
    /// </summary>
    private static PromptExecutionSettings ToPromptExecutionSettings(
        ToolCallingOptions options,
        IReadOnlyList<ToolCallingToolDefinition> tools)
    {
        var settings = new PromptExecutionSettings();

        if (options.Model != null)
        {
            settings.ModelId = options.Model;
        }

        settings.ExtensionData ??= new Dictionary<string, object>();

        if (options.Temperature.HasValue)
            settings.ExtensionData["temperature"] = options.Temperature.Value;

        if (options.MaxTokensPerCompletion.HasValue)
            settings.ExtensionData["max_tokens"] = options.MaxTokensPerCompletion.Value;

        // Handle tool calling configuration
        if (tools.Count > 0)
        {
            settings.ExtensionData["tools"] = tools.Select(t => new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = t.ParametersSchema
                }
            }).ToList();

            if (!string.IsNullOrEmpty(options.ToolChoice))
            {
                settings.ExtensionData["tool_choice"] = options.ToolChoice;
            }
        }

        return settings;
    }

    /// <summary>
    /// Extracts token usage from response metadata.
    /// </summary>
    private static (int promptTokens, int completionTokens) ExtractTokenUsage(
        IReadOnlyDictionary<string, object?>? metadata)
    {
        if (metadata?.TryGetValue("Usage", out var usageObj) == true &&
            usageObj is Dictionary<string, object?> usageDict)
        {
            var promptTokens = 0;
            var completionTokens = 0;

            if (usageDict.TryGetValue("InputTokens", out var inputTokens))
                promptTokens = Convert.ToInt32(inputTokens);

            if (usageDict.TryGetValue("OutputTokens", out var outputTokens))
                completionTokens = Convert.ToInt32(outputTokens);

            return (promptTokens, completionTokens);
        }

        return (0, 0);
    }
}

/// <summary>
/// Interface for emitting tool calling events.
/// </summary>
public interface IToolCallingEventEmitter
{
    /// <summary>
    /// Emits a tool calling event.
    /// </summary>
    void Emit(ToolCallingEvent @event);
}
