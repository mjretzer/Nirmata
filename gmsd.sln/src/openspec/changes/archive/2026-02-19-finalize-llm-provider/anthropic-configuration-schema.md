# Anthropic Configuration Schema

This document defines the configuration schema for Anthropic provider support in GMSD.

## Configuration Structure

```
GmsdAgents:SemanticKernel:
  Provider: "Anthropic"
  Anthropic:
    ApiKey: "sk-ant-..."
    ModelId: "claude-3-sonnet-20240229"
    BaseUrl: "https://api.anthropic.com" (optional)
    ApiVersion: "2023-06-01" (optional, default shown)
```

## Configuration Class

Located in `Gmsd.Agents/Configuration/SemanticKernelOptions.cs`:

```csharp
public sealed class AnthropicSemanticKernelOptions
{
    /// <summary>
    /// Anthropic API key. Must not be empty or whitespace.
    /// </summary>
    [Required(ErrorMessage = "Anthropic API key is required")]
    [MinLength(1, ErrorMessage = "Anthropic API key cannot be empty")]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model ID to use (e.g., "claude-3-opus-20240229", "claude-3-sonnet-20240229").
    /// </summary>
    [Required(ErrorMessage = "Anthropic model ID is required")]
    [MinLength(1, ErrorMessage = "Anthropic model ID cannot be empty")]
    public string ModelId { get; set; } = "claude-3-sonnet-20240229";

    /// <summary>
    /// Optional base URL override for Anthropic API.
    /// </summary>
    [Url(ErrorMessage = "BaseUrl must be a valid URL if provided")]
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Anthropic API version.
    /// Default: 2023-06-01
    /// </summary>
    public string ApiVersion { get; set; } = "2023-06-01";
}
```

## Configuration Fields

### Required Fields

#### ApiKey
- **Type**: string
- **Description**: Anthropic API key for authentication
- **Validation**: Required, non-empty
- **Example**: `sk-ant-v0-1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef`
- **Obtaining**: https://console.anthropic.com/account/keys

#### ModelId
- **Type**: string
- **Description**: Claude model identifier
- **Validation**: Required, non-empty
- **Default**: `claude-3-sonnet-20240229`
- **Supported Models**:
  - `claude-3-opus-20240229` - Most capable, highest cost
  - `claude-3-sonnet-20240229` - Balanced performance/cost (recommended)
  - `claude-3-haiku-20240307` - Fastest, lowest cost
  - `claude-3-5-sonnet-20241022` - Latest Sonnet version
  - `claude-3-5-haiku-20241022` - Latest Haiku version
- **Documentation**: https://docs.anthropic.com/claude/reference/models-overview

### Optional Fields

#### BaseUrl
- **Type**: string (URL)
- **Description**: Override default Anthropic API endpoint
- **Validation**: Valid URL format if provided
- **Default**: `https://api.anthropic.com`
- **Use Cases**: 
  - Proxy servers
  - Private/on-premises deployments
  - Testing with mock servers

#### ApiVersion
- **Type**: string
- **Description**: Anthropic API version
- **Validation**: Valid version string
- **Default**: `2023-06-01`
- **Supported Versions**: See https://docs.anthropic.com/claude/reference/versions

## Example Configurations

### Minimal Configuration (Production)
```json
{
  "GmsdAgents": {
    "SemanticKernel": {
      "Provider": "Anthropic",
      "Anthropic": {
        "ApiKey": "sk-ant-v0-...",
        "ModelId": "claude-3-sonnet-20240229"
      }
    }
  }
}
```

### Full Configuration (Development)
```json
{
  "GmsdAgents": {
    "SemanticKernel": {
      "Provider": "Anthropic",
      "Anthropic": {
        "ApiKey": "sk-ant-v0-...",
        "ModelId": "claude-3-opus-20240229",
        "BaseUrl": "https://api.anthropic.com",
        "ApiVersion": "2023-06-01"
      }
    }
  }
}
```

### With Environment Variables
```json
{
  "GmsdAgents": {
    "SemanticKernel": {
      "Provider": "Anthropic",
      "Anthropic": {
        "ApiKey": "${ANTHROPIC_API_KEY}",
        "ModelId": "claude-3-sonnet-20240229"
      }
    }
  }
}
```

## Validation Rules

### At Configuration Binding Time
- Provider must be "Anthropic" (case-insensitive)
- Anthropic section must be present
- ApiKey must be non-empty string
- ModelId must be non-empty string
- BaseUrl must be valid URL format if provided

### At DI Registration Time
- All required fields are validated with clear error messages
- Invalid configuration throws `InvalidOperationException` with guidance
- Errors include links to documentation for obtaining credentials

## Supported Features

### Chat Completion
- ✅ Single-turn chat
- ✅ Multi-turn conversations
- ✅ System prompts
- ✅ Temperature and sampling parameters
- ✅ Max tokens configuration

### Streaming
- ✅ Streaming responses
- ✅ Chunked delivery
- ✅ Cancellation support
- ✅ Token usage tracking

### Tool Calling
- ✅ Function definitions
- ✅ Tool invocation
- ✅ Parallel tool calls
- ✅ Tool result propagation

### Structured Outputs
- ✅ JSON schema validation
- ✅ Strict validation mode
- ✅ Schema caching
- ✅ Error reporting

## Error Handling

### Configuration Errors
- Missing ApiKey: Clear message with link to obtain key
- Missing ModelId: Clear message with supported models list
- Invalid BaseUrl: URL validation error
- Invalid ApiVersion: Version format error

### Runtime Errors
- Authentication failures: Retryable = false
- Rate limiting (429): Retryable = true
- Timeouts: Retryable = true
- Invalid model: Retryable = false
- Server errors (5xx): Retryable = true

## Migration Path

### From OpenAI to Anthropic
1. Update `Provider` to "Anthropic"
2. Add `Anthropic` section with ApiKey and ModelId
3. Remove or keep `OpenAi` section (won't be used)
4. Test with simple chat completion first
5. Verify tool calling works as expected
6. Monitor token usage and costs

### From Other Providers
Follow the same pattern:
1. Update Provider name
2. Add provider-specific configuration section
3. Test functionality
4. Monitor performance

## Performance Considerations

- **API calls**: ~200-500ms typical latency
- **Token limits**: Varies by model (see documentation)
- **Rate limits**: 50 requests/minute for free tier
- **Retry logic**: Exponential backoff (100ms base, max 3 retries)
- **Timeout**: 5 minutes per request

## Security Best Practices

1. **API Key Management**
   - Never commit API keys to version control
   - Use environment variables or secure vaults
   - Rotate keys regularly
   - Use least-privilege keys if available

2. **Configuration**
   - Use `appsettings.Production.json` for production
   - Override with environment variables
   - Don't log API keys or sensitive data
   - Use HTTPS for all API calls

3. **Monitoring**
   - Track API usage and costs
   - Monitor error rates
   - Alert on authentication failures
   - Log request/response metadata (not content)

## Testing

### Unit Tests
```csharp
[TestMethod]
public void ValidConfiguration_Loads_Successfully()
{
    var config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GmsdAgents:SemanticKernel:Provider"] = "Anthropic",
            ["GmsdAgents:SemanticKernel:Anthropic:ApiKey"] = "sk-ant-test",
            ["GmsdAgents:SemanticKernel:Anthropic:ModelId"] = "claude-3-sonnet-20240229"
        })
        .Build();

    var options = config.GetSection("GmsdAgents:SemanticKernel")
        .Get<SemanticKernelOptions>();

    Assert.IsNotNull(options?.Anthropic);
    Assert.AreEqual("sk-ant-test", options.Anthropic.ApiKey);
}
```

### Integration Tests
- Test chat completion with real API (requires API key)
- Test streaming responses
- Test tool calling
- Test error handling

## Troubleshooting

### "Anthropic API key is required"
- Ensure `GmsdAgents:SemanticKernel:Anthropic:ApiKey` is set
- Check for typos in configuration key path
- Verify environment variables are loaded

### "Anthropic model ID is required"
- Ensure `GmsdAgents:SemanticKernel:Anthropic:ModelId` is set
- Use one of the supported models from the list above

### "Invalid BaseUrl"
- Ensure BaseUrl is a valid HTTPS URL
- Example: `https://api.anthropic.com` or `https://proxy.example.com`

### Authentication failures
- Verify API key is valid and active
- Check key hasn't been rotated or revoked
- Ensure key has required permissions
- Check for typos in API key

### Rate limiting errors
- Reduce request frequency
- Implement backoff strategy (already in place)
- Consider upgrading to paid tier
- Batch requests if possible

## Future Enhancements

Potential additions to Anthropic configuration:
- Temperature and sampling parameters
- Max tokens per request
- Custom system prompts
- Vision/image support
- Batch processing configuration
- Cost tracking and budgeting
