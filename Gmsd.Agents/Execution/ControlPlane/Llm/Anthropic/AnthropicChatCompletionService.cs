using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gmsd.Agents.Execution.ControlPlane.Llm.Anthropic;

/// <summary>
/// Anthropic Claude chat completion service implementing Semantic Kernel's IChatCompletionService.
/// </summary>
public sealed class AnthropicChatCompletionService : IChatCompletionService
{
    private readonly AnthropicServiceConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AnthropicChatCompletionService>? _logger;

    /// <summary>
    /// Creates a new instance of the Anthropic chat completion service.
    /// </summary>
    /// <param name="config">The service configuration.</param>
    /// <param name="httpClient">The HTTP client for making API requests.</param>
    /// <param name="logger">Optional logger.</param>
    public AnthropicChatCompletionService(
        AnthropicServiceConfig config,
        HttpClient httpClient,
        ILogger<AnthropicChatCompletionService>? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>
    {
        ["ModelId"] = _config.ModelId,
        ["Provider"] = "Anthropic"
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var claudeSettings = executionSettings as ClaudePromptExecutionSettings
            ?? new ClaudePromptExecutionSettings();

        var requestBody = CreateRequestBody(chatHistory, claudeSettings, kernel);
        var json = JsonSerializer.Serialize(requestBody, AnthropicJsonContext.Default.ClaudeRequest);

        _logger?.LogDebug("Sending request to Anthropic API: {Request}", json);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        content.Headers.Add("x-api-key", _config.ApiKey);
        content.Headers.Add("anthropic-version", claudeSettings.AnthropicVersion);

        var stopwatch = Stopwatch.StartNew();
        var response = await _httpClient.PostAsync(
            "v1/messages",
            content,
            cancellationToken);
        stopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            var errorJson = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger?.LogError(
                "Anthropic API error: {StatusCode}, {Error}",
                response.StatusCode,
                errorJson);
            throw new AnthropicApiException(
                $"Anthropic API returned {response.StatusCode}: {errorJson}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger?.LogDebug("Received response from Anthropic API: {Response}", responseJson);

        var responseObject = JsonSerializer.Deserialize(
            responseJson,
            AnthropicJsonContext.Default.ClaudeResponse);

        if (responseObject?.Content == null || responseObject.Content.Count == 0)
        {
            throw new AnthropicApiException("Anthropic API returned empty response content");
        }

        var result = new List<ChatMessageContent>();

        foreach (var contentBlock in responseObject.Content)
        {
            if (contentBlock.Type == "text")
            {
                result.Add(new ChatMessageContent(
                    role: AuthorRole.Assistant,
                    content: contentBlock.Text)
                {
                    ModelId = responseObject.Model,
                    Metadata = new Dictionary<string, object?>
                    {
                        ["Id"] = responseObject.Id,
                        ["StopReason"] = responseObject.StopReason,
                        ["Usage.InputTokens"] = responseObject.Usage?.InputTokens,
                        ["Usage.OutputTokens"] = responseObject.Usage?.OutputTokens,
                        ["DurationMs"] = stopwatch.ElapsedMilliseconds
                    }
                });
            }
            else if (contentBlock.Type == "tool_use")
            {
                // Handle tool use block
                var arguments = new KernelArguments(ParseToolInput(contentBlock.Input));
                var functionCallContent = new FunctionCallContent(
                    functionName: contentBlock.Name ?? "",
                    pluginName: null,
                    arguments: arguments);

                var messageContent = new ChatMessageContent(
                    role: AuthorRole.Assistant,
                    items: [functionCallContent])
                {
                    ModelId = responseObject.Model,
                    Metadata = new Dictionary<string, object?>
                    {
                        ["Id"] = responseObject.Id,
                        ["ToolUseId"] = contentBlock.Id,
                        ["StopReason"] = responseObject.StopReason,
                        ["Usage.InputTokens"] = responseObject.Usage?.InputTokens,
                        ["Usage.OutputTokens"] = responseObject.Usage?.OutputTokens
                    }
                };

                result.Add(messageContent);
            }
        }

        // If no results were added, add an empty assistant message
        if (result.Count == 0)
        {
            result.Add(new ChatMessageContent(
                role: AuthorRole.Assistant,
                content: "")
            {
                ModelId = responseObject.Model,
                Metadata = new Dictionary<string, object?>
                {
                    ["Id"] = responseObject.Id,
                    ["StopReason"] = responseObject.StopReason,
                    ["Usage.InputTokens"] = responseObject.Usage?.InputTokens,
                    ["Usage.OutputTokens"] = responseObject.Usage?.OutputTokens
                }
            });
        }

        return result;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // For now, implement as non-streaming and yield results
        // Full streaming implementation would use SSE parsing
        var results = await GetChatMessageContentsAsync(
            chatHistory,
            executionSettings,
            kernel,
            cancellationToken);

        foreach (var result in results)
        {
            yield return new StreamingChatMessageContent(
                role: result.Role,
                content: result.Content,
                choiceIndex: 0,
                modelId: result.ModelId,
                metadata: result.Metadata);
        }
    }

    private ClaudeRequest CreateRequestBody(
        ChatHistory chatHistory,
        ClaudePromptExecutionSettings settings,
        Kernel? kernel)
    {
        var messages = MapChatHistoryToClaudeMessages(chatHistory);

        // Extract system message if present
        var systemMessage = chatHistory.FirstOrDefault(m => m.Role == AuthorRole.System);
        var system = settings.System ?? systemMessage?.Content;

        // Build tools from kernel if available
        var tools = BuildToolsFromKernel(kernel);

        return new ClaudeRequest
        {
            Model = _config.ModelId,
            MaxTokens = settings.MaxTokens,
            Temperature = settings.Temperature,
            TopK = settings.TopK,
            TopP = settings.TopP,
            System = system,
            Messages = messages,
            Tools = tools?.Count > 0 ? tools : null,
            ToolChoice = settings.ToolChoice,
            Stream = false
        };
    }

    private static List<ClaudeMessage> MapChatHistoryToClaudeMessages(ChatHistory chatHistory)
    {
        var messages = new List<ClaudeMessage>();

        foreach (var message in chatHistory)
        {
            // Skip system messages - they're handled separately
            if (message.Role == AuthorRole.System)
            {
                continue;
            }

            var role = MapAuthorRoleToClaudeRole(message.Role);
            var content = MapMessageContent(message);

            messages.Add(new ClaudeMessage
            {
                Role = role,
                Content = content
            });
        }

        return messages;
    }

    private static string MapAuthorRoleToClaudeRole(AuthorRole role)
    {
        return role.Label.ToLowerInvariant() switch
        {
            "user" => "user",
            "assistant" => "assistant",
            _ => "user"
        };
    }

    private static List<ClaudeContentBlock> MapMessageContent(ChatMessageContent message)
    {
        var blocks = new List<ClaudeContentBlock>();

        // Handle function call results (tool results)
        var toolResultItems = message.Items?.OfType<FunctionResultContent>().ToList();
        if (toolResultItems?.Count > 0)
        {
            foreach (var toolResult in toolResultItems)
            {
                blocks.Add(new ClaudeContentBlock
                {
                    Type = "tool_result",
                    ToolUseId = toolResult.CallId ?? toolResult.FunctionName,
                    Content = toolResult.Result?.ToString() ?? ""
                });
            }
        }
        else if (message.Items?.Count > 0)
        {
            // Handle multi-item content
            foreach (var item in message.Items)
            {
                switch (item)
                {
                    case TextContent textContent:
                        blocks.Add(new ClaudeContentBlock
                        {
                            Type = "text",
                            Text = textContent.Text ?? ""
                        });
                        break;

                    case FunctionCallContent functionCall:
                        blocks.Add(new ClaudeContentBlock
                        {
                            Type = "tool_use",
                            Id = functionCall.Id ?? Guid.NewGuid().ToString(),
                            Name = functionCall.FunctionName,
                            Input = SerializeArguments(functionCall.Arguments)
                        });
                        break;
                }
            }
        }
        else
        {
            // Simple text content
            blocks.Add(new ClaudeContentBlock
            {
                Type = "text",
                Text = message.Content ?? ""
            });
        }

        return blocks;
    }

    private static Dictionary<string, object?> ParseToolInput(Dictionary<string, object?>? input)
    {
        return input ?? new Dictionary<string, object?>();
    }

    private static Dictionary<string, object?>? SerializeArguments(IReadOnlyDictionary<string, object?>? arguments)
    {
        if (arguments == null || arguments.Count == 0)
        {
            return null;
        }

        return arguments.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private static List<ClaudeToolDefinition>? BuildToolsFromKernel(Kernel? kernel)
    {
        if (kernel?.Plugins?.Count == 0)
        {
            return null;
        }

        var tools = new List<ClaudeToolDefinition>();

        foreach (var plugin in kernel?.Plugins ?? [])
        {
            foreach (var function in plugin)
            {
                var schema = MapKernelFunctionParametersToSchema(function.Metadata.Parameters);

                tools.Add(new ClaudeToolDefinition
                {
                    Name = $"{plugin.Name}_{function.Name}",
                    Description = function.Metadata.Description ?? $"Function {function.Name}",
                    InputSchema = schema
                });
            }
        }

        return tools.Count > 0 ? tools : null;
    }

    private static ClaudeInputSchema MapKernelFunctionParametersToSchema(
        IReadOnlyList<KernelParameterMetadata> parameters)
    {
        var properties = new Dictionary<string, ClaudeSchemaProperty>();
        var required = new List<string>();

        foreach (var param in parameters)
        {
            var property = new ClaudeSchemaProperty
            {
                Type = MapTypeToJsonSchemaType(param.ParameterType),
                Description = param.Description ?? $"Parameter {param.Name}"
            };

            properties[param.Name] = property;

            if (!param.IsRequired)
            {
                continue;
            }
            
            required.Add(param.Name);
        }

        return new ClaudeInputSchema
        {
            Type = "object",
            Properties = properties.Count > 0 ? properties : null,
            Required = required.Count > 0 ? required : null
        };
    }

    private static string MapTypeToJsonSchemaType(Type? type)
    {
        if (type == null)
        {
            return "string";
        }

        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        return underlyingType switch
        {
            var t when t == typeof(string) => "string",
            var t when t == typeof(int) || t == typeof(long) || t == typeof(short) => "integer",
            var t when t == typeof(double) || t == typeof(float) || t == typeof(decimal) => "number",
            var t when t == typeof(bool) => "boolean",
            var t when t.IsArray || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>)) => "array",
            _ => "object"
        };
    }
}

/// <summary>
/// Configuration for the Anthropic service.
/// </summary>
public sealed class AnthropicServiceConfig
{
    /// <summary>
    /// The Anthropic API key.
    /// </summary>
    public required string ApiKey { get; set; }

    /// <summary>
    /// The model ID to use (e.g., "claude-3-opus-20240229").
    /// </summary>
    public required string ModelId { get; set; }

    /// <summary>
    /// Optional base URL override.
    /// </summary>
    public string? BaseUrl { get; set; }
}

/// <summary>
/// Exception thrown when the Anthropic API returns an error.
/// </summary>
public sealed class AnthropicApiException : Exception
{
    public AnthropicApiException(string message) : base(message) { }
    public AnthropicApiException(string message, Exception inner) : base(message, inner) { }
}

// JSON Serialization Context for Anthropic API
[JsonSerializable(typeof(ClaudeRequest))]
[JsonSerializable(typeof(ClaudeResponse))]
[JsonSerializable(typeof(ClaudeMessage))]
[JsonSerializable(typeof(ClaudeContentBlock))]
[JsonSerializable(typeof(ClaudeUsage))]
[JsonSerializable(typeof(ClaudeToolDefinition))]
[JsonSerializable(typeof(ClaudeInputSchema))]
[JsonSerializable(typeof(ClaudeSchemaProperty))]
[JsonSerializable(typeof(ClaudeToolChoice))]
internal partial class AnthropicJsonContext : JsonSerializerContext { }

// Request/Response Models
internal sealed class ClaudeRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; set; }

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("top_k")]
    public int? TopK { get; set; }

    [JsonPropertyName("top_p")]
    public double TopP { get; set; } = 1.0;

    [JsonPropertyName("system")]
    public string? System { get; set; }

    [JsonPropertyName("messages")]
    public required List<ClaudeMessage> Messages { get; set; }

    [JsonPropertyName("tools")]
    public List<ClaudeToolDefinition>? Tools { get; set; }

    [JsonPropertyName("tool_choice")]
    public ClaudeToolChoice? ToolChoice { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
}

internal sealed class ClaudeResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("role")]
    public required string Role { get; set; }

    [JsonPropertyName("model")]
    public required string Model { get; set; }

    [JsonPropertyName("content")]
    public required List<ClaudeContentBlock> Content { get; set; }

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }

    [JsonPropertyName("stop_sequence")]
    public string? StopSequence { get; set; }

    [JsonPropertyName("usage")]
    public ClaudeUsage? Usage { get; set; }
}

internal sealed class ClaudeMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; set; }

    [JsonPropertyName("content")]
    public required List<ClaudeContentBlock> Content { get; set; }
}

internal sealed class ClaudeContentBlock
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("input")]
    public Dictionary<string, object?>? Input { get; set; }

    [JsonPropertyName("tool_use_id")]
    public string? ToolUseId { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

internal sealed class ClaudeUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
}
