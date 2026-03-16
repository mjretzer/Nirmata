# Provider Expansion Guide - finalize-llm-provider

This document provides a comprehensive guide for expanding LLM provider support in nirmata using Semantic Kernel.

## Overview

The nirmata Agents system uses a **configuration-driven provider selection pattern** that allows switching between different LLM providers without code changes. This guide explains the architecture and provides step-by-step instructions for adding new providers.

## Architecture

### Provider Selection Flow

```
appsettings.json (nirmataAgents:SemanticKernel:Provider)
        ↓
SemanticKernelOptions (configuration binding)
        ↓
ValidateSemanticKernelConfiguration() (startup validation)
        ↓
AddProviderSpecificChatCompletion() (DI registration)
        ↓
Kernel.GetRequiredService<IChatCompletionService>()
        ↓
SemanticKernelLlmProvider (ILlmProvider adapter)
        ↓
Workflows (Planners, Chat Responder, Tool Calling)
```

### Key Components

1. **SemanticKernelOptions** (`nirmata.Agents/Configuration/SemanticKernelOptions.cs`)
   - Central configuration class with provider selection
   - Contains provider-specific options classes
   - Validated at DI registration time

2. **SemanticKernelServiceCollectionExtensions** (`nirmata.Agents/Configuration/SemanticKernelServiceCollectionExtensions.cs`)
   - Registers Semantic Kernel services
   - Validates configuration for selected provider
   - Instantiates provider-specific chat completion service

3. **SemanticKernelLlmProvider** (`nirmata.Agents/Execution/ControlPlane/Llm/Adapters/SemanticKernelLlmProvider.cs`)
   - Provider-agnostic adapter
   - Implements ILlmProvider interface
   - Delegates to SK's IChatCompletionService
   - Handles structured outputs, streaming, tool calling

## Currently Supported Providers

### OpenAI
- **Models**: GPT-4, GPT-4 Turbo, GPT-3.5-Turbo, GPT-4o
- **Configuration**: `nirmataAgents:SemanticKernel:Provider = "OpenAi"`
- **Required fields**: ApiKey, ModelId
- **Optional fields**: OrganizationId, BaseUrl, Temperature, MaxTokens, TopP, FrequencyPenalty, PresencePenalty, Seed

### Azure OpenAI
- **Models**: Same as OpenAI (deployed to Azure)
- **Configuration**: `nirmataAgents:SemanticKernel:Provider = "AzureOpenAi"`
- **Required fields**: Endpoint, ApiKey, DeploymentName
- **Optional fields**: ApiVersion (default: 2024-02-01)

### Anthropic
- **Models**: Claude 3 family (Opus, Sonnet, Haiku)
- **Configuration**: `nirmataAgents:SemanticKernel:Provider = "Anthropic"`
- **Required fields**: ApiKey, ModelId
- **Optional fields**: BaseUrl, ApiVersion (default: 2023-06-01)

### Ollama
- **Models**: Llama, Mistral, CodeLlama, etc.
- **Configuration**: `nirmataAgents:SemanticKernel:Provider = "Ollama"`
- **Required fields**: ModelId
- **Optional fields**: BaseUrl (default: http://localhost:11434)

## Adding a New Provider

### Step 1: Create Configuration Options Class

Create a new sealed class in `nirmata.Agents/Configuration/SemanticKernelOptions.cs`:

```csharp
/// <summary>
/// Configuration options for [NewProvider] connector in Semantic Kernel.
/// </summary>
public sealed class NewProviderSemanticKernelOptions
{
    /// <summary>
    /// [NewProvider] API key. Must not be empty or whitespace.
    /// </summary>
    [Required(ErrorMessage = "[NewProvider] API key is required")]
    [MinLength(1, ErrorMessage = "[NewProvider] API key cannot be empty")]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model ID to use (e.g., "model-name-v1").
    /// </summary>
    [Required(ErrorMessage = "[NewProvider] model ID is required")]
    [MinLength(1, ErrorMessage = "[NewProvider] model ID cannot be empty")]
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Optional base URL override for [NewProvider] API.
    /// </summary>
    [Url(ErrorMessage = "BaseUrl must be a valid URL if provided")]
    public string? BaseUrl { get; set; }

    // Add other provider-specific settings as needed
}
```

### Step 2: Update SemanticKernelOptions

Add the new provider to `SemanticKernelOptions` class:

1. Update the `Provider` property regex to include the new provider:
```csharp
[RegularExpression(@"^(OpenAi|AzureOpenAi|Ollama|Anthropic|NewProvider)$", 
    ErrorMessage = "Provider must be one of: OpenAi, AzureOpenAi, Ollama, Anthropic, NewProvider")]
```

2. Add a property for the new provider options:
```csharp
/// <summary>
/// NewProvider-specific options (used when Provider = "NewProvider").
/// </summary>
public NewProviderSemanticKernelOptions? NewProvider { get; set; }
```

### Step 3: Add Validation Method

In `SemanticKernelServiceCollectionExtensions.cs`, add a validation method:

```csharp
/// <summary>
/// Validates [NewProvider]-specific configuration.
/// </summary>
private static void ValidateNewProviderConfiguration(
    NewProviderSemanticKernelOptions? options, 
    IConfiguration section)
{
    if (options is null)
    {
        throw new InvalidOperationException(
            "[NewProvider] configuration is missing. " +
            "Ensure 'nirmataAgents:SemanticKernel:NewProvider' section is configured with ApiKey and ModelId. " +
            "Example: {\"nirmataAgents\": {\"SemanticKernel\": {\"NewProvider\": {\"ApiKey\": \"...\", \"ModelId\": \"...\"}}}}");
    }

    if (string.IsNullOrWhiteSpace(options.ApiKey))
    {
        throw new InvalidOperationException(
            "[NewProvider] API key is required but not configured. " +
            "Ensure 'nirmataAgents:SemanticKernel:NewProvider:ApiKey' is set to your [NewProvider] API key. " +
            "You can obtain one from [provider documentation URL]");
    }

    if (string.IsNullOrWhiteSpace(options.ModelId))
    {
        throw new InvalidOperationException(
            "[NewProvider] model ID is required but not configured. " +
            "Ensure 'nirmataAgents:SemanticKernel:NewProvider:ModelId' is set to a valid model. " +
            "See [provider documentation URL] for available models.");
    }
}
```

Then add a case to `ValidateSemanticKernelConfiguration()`:
```csharp
case "newprovider":
    ValidateNewProviderConfiguration(options.NewProvider, section);
    break;
```

### Step 4: Implement Chat Completion Registration

Add a method to register the provider with Semantic Kernel:

```csharp
/// <summary>
/// Adds [NewProvider] chat completion service to the kernel builder.
/// </summary>
public static IKernelBuilder AddNewProviderChatCompletion(
    this IKernelBuilder builder,
    IConfiguration configuration)
{
    var options = configuration
        .GetSection("NewProvider")
        .Get<NewProviderSemanticKernelOptions>();

    if (options is null)
    {
        throw new InvalidOperationException(
            "[NewProvider] configuration is missing. " +
            "Ensure 'nirmataAgents:SemanticKernel:NewProvider' section is configured.");
    }

    if (string.IsNullOrWhiteSpace(options.ApiKey))
    {
        throw new InvalidOperationException(
            "[NewProvider] API key is required. " +
            "Ensure 'nirmataAgents:SemanticKernel:NewProvider:ApiKey' is set.");
    }

    if (string.IsNullOrWhiteSpace(options.ModelId))
    {
        throw new InvalidOperationException(
            "[NewProvider] model ID is required. " +
            "Ensure 'nirmataAgents:SemanticKernel:NewProvider:ModelId' is set.");
    }

    // Register execution settings if needed
    var executionSettings = new NewProviderPromptExecutionSettings
    {
        ModelId = options.ModelId
    };
    builder.Services.AddSingleton(executionSettings);

    // Use Semantic Kernel's connector for [NewProvider]
    // Example: builder.AddNewProviderChatCompletion(modelId: options.ModelId, apiKey: options.ApiKey);
    
    return builder;
}
```

Then add a case to `AddProviderSpecificChatCompletion()`:
```csharp
case "newprovider":
    builder.AddNewProviderChatCompletion(configuration);
    break;
```

### Step 5: Write Comprehensive Tests

Create tests in `tests/nirmata.Agents.Tests/Configuration/`:

```csharp
[TestClass]
public class NewProviderSemanticKernelOptionsTests
{
    [TestMethod]
    public void ValidConfiguration_Loads_Successfully()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["nirmataAgents:SemanticKernel:Provider"] = "NewProvider",
                ["nirmataAgents:SemanticKernel:NewProvider:ApiKey"] = "test-key",
                ["nirmataAgents:SemanticKernel:NewProvider:ModelId"] = "test-model"
            })
            .Build();

        // Act
        var options = config.GetSection("nirmataAgents:SemanticKernel").Get<SemanticKernelOptions>();

        // Assert
        Assert.IsNotNull(options);
        Assert.AreEqual("NewProvider", options.Provider);
        Assert.IsNotNull(options.NewProvider);
        Assert.AreEqual("test-key", options.NewProvider.ApiKey);
        Assert.AreEqual("test-model", options.NewProvider.ModelId);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void MissingApiKey_Throws_InvalidOperationException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["nirmataAgents:SemanticKernel:Provider"] = "NewProvider",
                ["nirmataAgents:SemanticKernel:NewProvider:ModelId"] = "test-model"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddSemanticKernel(config);
    }
}
```

Create integration tests in `tests/nirmata.Agents.Tests/Execution/ControlPlane/Llm/`:

```csharp
[TestClass]
public class NewProviderChatCompletionTests
{
    [TestMethod]
    public async Task ChatCompletion_Returns_ValidResponse()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["nirmataAgents:SemanticKernel:Provider"] = "NewProvider",
                ["nirmataAgents:SemanticKernel:NewProvider:ApiKey"] = Environment.GetEnvironmentVariable("NEWPROVIDER_API_KEY") ?? "test-key",
                ["nirmataAgents:SemanticKernel:NewProvider:ModelId"] = "test-model"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSemanticKernel(config);
        services.AddLogging();
        var provider = services.BuildServiceProvider();

        var llmProvider = provider.GetRequiredService<ILlmProvider>();
        var request = new LlmCompletionRequest
        {
            Messages = new[] { new LlmMessage { Role = LlmMessageRole.User, Content = "Hello" } },
            Model = "test-model"
        };

        // Act
        var response = await llmProvider.CompleteAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNotNull(response.Message);
        Assert.IsNotNull(response.Message.Content);
    }

    [TestMethod]
    public async Task Streaming_Returns_AllChunks()
    {
        // Similar to above but test streaming
    }

    [TestMethod]
    public async Task ToolCalling_Works_Correctly()
    {
        // Test tool calling with the new provider
    }
}
```

### Step 6: Update Configuration Examples

Update `appsettings.Development.json`:

```json
{
  "nirmataAgents": {
    "SemanticKernel": {
      "Provider": "NewProvider",
      "NewProvider": {
        "ApiKey": "your-api-key-here",
        "ModelId": "model-name",
        "BaseUrl": "https://api.newprovider.com"
      }
    }
  }
}
```

### Step 7: Document Provider-Specific Behavior

Create a provider-specific documentation file if needed, documenting:
- Supported models and their capabilities
- Tool calling limitations (if any)
- Streaming behavior
- Error handling specifics
- Rate limiting and quotas
- Cost considerations

## Anthropic Example Implementation

The Anthropic provider was added following this pattern:

1. **Configuration**: `AnthropicSemanticKernelOptions` with ApiKey, ModelId, BaseUrl, ApiVersion
2. **Validation**: `ValidateAnthropicConfiguration()` with clear error messages
3. **Registration**: `AddAnthropicChatCompletion()` using custom SK connector
4. **Tests**: Configuration and integration tests for chat, streaming, and tool calling
5. **Documentation**: Example configuration and troubleshooting guide

Implementation time: ~2 days following this pattern.

## Testing Requirements for New Providers

### Unit Tests
- Configuration validation (missing fields, invalid values)
- DI registration success and failure cases
- Error handling for provider-specific exceptions

### Integration Tests
- Basic chat completion
- Streaming responses
- Tool calling (single and multiple)
- Structured outputs
- Error recovery and retries
- Token usage tracking

### End-to-End Tests
- Multi-turn conversations
- Complex tool scenarios
- Evidence capture
- Performance benchmarks

## Common Implementation Patterns

### Pattern 1: Using Official SK Connector
If Semantic Kernel has an official connector:
```csharp
builder.AddNewProviderChatCompletion(
    modelId: options.ModelId,
    apiKey: options.ApiKey);
```

### Pattern 2: Using Community Connector
If using a community SK connector:
```csharp
var client = new NewProviderClient(options.ApiKey);
builder.Services.AddSingleton<IChatCompletionService>(
    new NewProviderChatCompletionService(client, options.ModelId));
```

### Pattern 3: Custom Adapter
If no SK connector exists:
```csharp
var httpClient = new HttpClient { BaseAddress = new Uri(options.BaseUrl ?? "https://api.newprovider.com") };
builder.Services.AddSingleton<IChatCompletionService>(
    new CustomNewProviderAdapter(httpClient, options.ApiKey, options.ModelId));
```

## Troubleshooting Provider Integration

### Configuration Not Recognized
- Verify provider name matches case-insensitively
- Check regex pattern in SemanticKernelOptions.Provider
- Review validation error message

### Startup Validation Fails
- Check all required configuration fields are present
- Verify field values are valid (non-empty strings, valid URLs)
- Review provider-specific validation method

### Chat Completion Fails
- Verify API key is valid and has required permissions
- Check model ID is supported by the provider
- Review provider-specific error handling
- Check network connectivity

### Tool Calling Not Working
- Verify provider supports function calling
- Test with simple tool first
- Check tool schema is valid JSON
- Review provider documentation for limitations

### Streaming Issues
- Test with shorter responses first
- Verify cancellation handling
- Check for chunking issues
- Review provider streaming API documentation

## Performance Considerations

- **Schema validation**: Target < 50ms per validation
- **Token usage tracking**: Ensure metadata is captured
- **Connection pooling**: Consider for high-volume scenarios
- **Retry logic**: Exponential backoff with jitter (100ms base, max 3 retries)
- **Timeout handling**: Set appropriate timeouts for provider

## Security Considerations

- **API keys**: Never log or expose in error messages
- **Prompts**: Don't log sensitive user data
- **Configuration**: Use environment variables for secrets
- **HTTPS**: Always use HTTPS for API calls
- **Validation**: Validate all inputs before sending to provider

## Future Expansion Roadmap

Potential providers for future consideration:
- **Cohere**: Alternative text generation
- **Hugging Face**: Open-source model access
- **Gemini**: Google's multimodal model
- **Mistral**: Open-source alternative
- **LLaMA 2**: Via Ollama or dedicated connector
- **Local models**: Additional local inference options
