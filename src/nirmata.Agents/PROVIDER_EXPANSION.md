# LLM Provider Expansion Guide

This guide explains how to add support for new LLM providers to the nirmata Agents system using Semantic Kernel.

## Architecture Overview

The system uses a **provider selection pattern** based on configuration:

1. **Configuration-driven selection**: The active provider is determined by `nirmataAgents:SemanticKernel:Provider` setting
2. **Semantic Kernel abstraction**: All providers implement SK's `IChatCompletionService` interface
3. **Unified adapter**: `SemanticKernelLlmProvider` adapts SK's interface to the internal `ILlmProvider` contract
4. **Dependency injection**: Provider-specific services are registered based on the configured provider

## Current Supported Providers

- **OpenAI**: GPT-4, GPT-4 Turbo, GPT-3.5-Turbo
- **Azure OpenAI**: Enterprise Azure deployments
- **Anthropic**: Claude 3 family (Opus, Sonnet, Haiku)
- **Ollama**: Local models (Llama, Mistral, etc.)

## Adding a New Provider

### Step 1: Create Configuration Options Class

Create a new configuration class in `nirmata.Agents/Configuration/`:

```csharp
public sealed class NewProviderSemanticKernelOptions
{
    [Required(ErrorMessage = "API key is required")]
    [MinLength(1, ErrorMessage = "API key cannot be empty")]
    public string ApiKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "Model ID is required")]
    [MinLength(1, ErrorMessage = "Model ID cannot be empty")]
    public string ModelId { get; set; } = string.Empty;

    // Add provider-specific settings as needed
    public string? BaseUrl { get; set; }
}
```

### Step 2: Add Configuration to SemanticKernelOptions

Update `nirmata.Agents/Configuration/SemanticKernelOptions.cs`:

1. Add the new provider to the `Provider` property regex validation
2. Add a property for the new provider options:

```csharp
/// <summary>
/// NewProvider-specific options (used when Provider = "NewProvider").
/// </summary>
public NewProviderSemanticKernelOptions? NewProvider { get; set; }
```

### Step 3: Implement Validation

In `SemanticKernelServiceCollectionExtensions.cs`, add:

1. A validation method:
```csharp
private static void ValidateNewProviderConfiguration(
    NewProviderSemanticKernelOptions? options, 
    IConfiguration section)
{
    if (options is null)
    {
        throw new InvalidOperationException(
            "NewProvider configuration is missing. " +
            "Ensure 'nirmataAgents:SemanticKernel:NewProvider' section is configured.");
    }

    if (string.IsNullOrWhiteSpace(options.ApiKey))
    {
        throw new InvalidOperationException(
            "NewProvider API key is required. " +
            "Ensure 'nirmataAgents:SemanticKernel:NewProvider:ApiKey' is set.");
    }

    if (string.IsNullOrWhiteSpace(options.ModelId))
    {
        throw new InvalidOperationException(
            "NewProvider model ID is required. " +
            "Ensure 'nirmataAgents:SemanticKernel:NewProvider:ModelId' is set.");
    }
}
```

2. Add case to `ValidateSemanticKernelConfiguration()` switch statement:
```csharp
case "newprovider":
    ValidateNewProviderConfiguration(options.NewProvider, section);
    break;
```

### Step 4: Implement Chat Completion Registration

Add a method to register the provider with SK:

```csharp
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
            "NewProvider configuration is missing.");
    }

    // Use Semantic Kernel's connector for NewProvider
    // Example: builder.AddNewProviderChatCompletion(...)
    
    return builder;
}
```

3. Add case to `AddProviderSpecificChatCompletion()` switch statement:
```csharp
case "newprovider":
    builder.AddNewProviderChatCompletion(configuration);
    break;
```

### Step 5: Test the Integration

1. **Unit tests**: Create tests in `tests/nirmata.Agents.Tests/Configuration/`
   - Test configuration validation
   - Test DI registration
   - Test error handling for missing/invalid config

2. **Integration tests**: Create tests in `tests/nirmata.Agents.Tests/Execution/ControlPlane/Llm/`
   - Test chat completion with the new provider
   - Test streaming responses
   - Test tool calling
   - Test structured outputs

3. **Configuration tests**: Verify the provider can be selected via configuration

### Step 6: Update Configuration Examples

Update `appsettings.Development.json` and documentation with example configuration:

```json
{
  "nirmataAgents": {
    "SemanticKernel": {
      "Provider": "NewProvider",
      "NewProvider": {
        "ApiKey": "your-api-key",
        "ModelId": "model-name",
        "BaseUrl": "https://api.newprovider.com"
      }
    }
  }
}
```

## Provider Selection Pattern

The provider selection follows this flow:

```
Configuration (appsettings.json)
    ↓
SemanticKernelOptions.Provider
    ↓
ValidateSemanticKernelConfiguration() [validates provider-specific config]
    ↓
AddProviderSpecificChatCompletion() [registers provider with SK]
    ↓
Kernel.GetRequiredService<IChatCompletionService>()
    ↓
SemanticKernelLlmProvider [adapts to ILlmProvider]
    ↓
Workflows (Planners, Chat Responder, etc.)
```

## Key Design Principles

1. **Configuration-driven**: No code changes needed to switch providers
2. **Validation at startup**: Configuration errors are caught early with clear messages
3. **Semantic Kernel abstraction**: Leverages SK's vendor-neutral interfaces
4. **Minimal adapter code**: `SemanticKernelLlmProvider` is provider-agnostic
5. **Extensible**: New providers can be added without modifying existing code

## Common Pitfalls

1. **Missing validation**: Always validate provider-specific configuration
2. **Incorrect case sensitivity**: Provider names are case-insensitive in validation
3. **Missing error handling**: Catch provider-specific exceptions and convert to `LlmProviderException`
4. **Incomplete tool support**: Verify tool calling works with the new provider
5. **Streaming issues**: Test streaming responses thoroughly

## Worked Example: Anthropic

Anthropic was added following this pattern:

1. Created `AnthropicSemanticKernelOptions` with ApiKey, ModelId, BaseUrl, ApiVersion
2. Added validation in `ValidateAnthropicConfiguration()`
3. Implemented `AddAnthropicChatCompletion()` using SK's Anthropic connector
4. Added comprehensive tests for configuration and chat completion
5. Updated documentation with example configuration

The implementation took approximately 2 days following this pattern.

## Troubleshooting

### Provider not recognized
- Check `SemanticKernelOptions.Provider` regex validation
- Ensure provider name matches case-insensitively

### Configuration validation fails at startup
- Verify all required fields are present in configuration
- Check error message for specific missing/invalid field
- Refer to provider-specific validation method

### Chat completion fails
- Check API key is valid and has required permissions
- Verify model ID is supported by the provider
- Check network connectivity and firewall rules
- Review logs for provider-specific error messages

### Tool calling not working
- Verify provider supports tool/function calling
- Check tool schema is valid JSON
- Test with a simple tool first
- Review provider documentation for tool calling limitations

## Future Providers

Candidates for future expansion:
- **Cohere**: For alternative text generation
- **Hugging Face**: For open-source model access
- **LLaMA 2**: Via Ollama or dedicated connector
- **Gemini**: Google's multimodal model
- **Mistral**: Open-source alternative
