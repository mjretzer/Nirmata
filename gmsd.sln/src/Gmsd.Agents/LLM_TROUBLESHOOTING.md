# LLM Provider Troubleshooting Guide

This guide helps diagnose and resolve common issues with the Semantic Kernel LLM provider integration.

## Configuration Issues

### "Semantic Kernel configuration is missing"

**Cause**: The `GmsdAgents:SemanticKernel` section is not present in `appsettings.json`.

**Solution**:
1. Add the configuration section to your `appsettings.json`:
```json
{
  "GmsdAgents": {
    "SemanticKernel": {
      "Provider": "OpenAi",
      "OpenAi": {
        "ApiKey": "sk-...",
        "ModelId": "gpt-4"
      }
    }
  }
}
```

2. Verify the file is in the correct location (root of the project or appropriate environment-specific file)
3. Ensure JSON syntax is valid (use a JSON validator if needed)

### "Provider is required but not configured"

**Cause**: The `Provider` field is missing or empty in the configuration.

**Solution**:
1. Set the `Provider` field to one of the supported providers:
   - `OpenAi` - for OpenAI models
   - `AzureOpenAi` - for Azure OpenAI
   - `Anthropic` - for Claude models
   - `Ollama` - for local models

2. Example:
```json
{
  "GmsdAgents": {
    "SemanticKernel": {
      "Provider": "OpenAi"
    }
  }
}
```

### "API key is required but not configured"

**Cause**: The API key field for the selected provider is missing or empty.

**Solution**:
1. Obtain your API key from the provider:
   - **OpenAI**: https://platform.openai.com/account/api-keys
   - **Azure OpenAI**: Azure Portal → OpenAI resource → Keys and Endpoint
   - **Anthropic**: https://console.anthropic.com/account/keys
   - **Ollama**: Not required (local)

2. Add the API key to your configuration:
```json
{
  "GmsdAgents": {
    "SemanticKernel": {
      "Provider": "OpenAi",
      "OpenAi": {
        "ApiKey": "sk-your-actual-key-here",
        "ModelId": "gpt-4"
      }
    }
  }
}
```

3. **Security best practice**: Use environment variables or secrets management instead of hardcoding:
```csharp
// In Program.cs
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") 
    ?? throw new InvalidOperationException("OPENAI_API_KEY not set");

builder.Configuration["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = apiKey;
```

### "Model ID is required but not configured"

**Cause**: The `ModelId` field is missing or empty.

**Solution**:
1. Set a valid model ID for your provider:
   - **OpenAI**: `gpt-4`, `gpt-4-turbo`, `gpt-4o`, `gpt-3.5-turbo`
   - **Azure OpenAI**: Your deployment name
   - **Anthropic**: `claude-3-opus-20240229`, `claude-3-sonnet-20240229`, `claude-3-haiku-20240307`
   - **Ollama**: `llama3`, `mistral`, `codellama`, etc.

2. Example:
```json
{
  "GmsdAgents": {
    "SemanticKernel": {
      "Provider": "OpenAi",
      "OpenAi": {
        "ApiKey": "sk-...",
        "ModelId": "gpt-4"
      }
    }
  }
}
```

### "Model is not in the list of supported models"

**Cause**: The specified model is not supported or the model name is incorrect.

**Solution**:
1. Verify the model name is spelled correctly
2. Check the provider's documentation for available models
3. Use a supported model:
   - For OpenAI: Use `gpt-4`, `gpt-4-turbo`, `gpt-4o`, or `gpt-3.5-turbo`
   - For Anthropic: Use Claude 3 family models
   - For Ollama: Ensure the model is installed locally

## Runtime Issues

### "Failed to complete chat" / Connection errors

**Cause**: Network connectivity issue or API endpoint unreachable.

**Symptoms**:
- `HttpRequestException`
- `TimeoutException`
- Connection refused errors

**Solution**:
1. **Check network connectivity**:
   ```bash
   ping api.openai.com  # For OpenAI
   ping your-resource.openai.azure.com  # For Azure
   ```

2. **Verify API endpoint**:
   - Ensure the endpoint URL is correct in configuration
   - Check firewall/proxy settings
   - Verify VPN connection if required

3. **Check API status**:
   - Visit the provider's status page
   - OpenAI: https://status.openai.com
   - Azure: Azure Portal status
   - Anthropic: https://status.anthropic.com

4. **Increase timeout** (if requests are timing out):
```json
{
  "GmsdAgents": {
    "SemanticKernel": {
      "OpenAi": {
        "TimeoutSeconds": 60
      }
    }
  }
}
```

### "Unauthorized" / Authentication errors

**Cause**: Invalid or expired API key.

**Symptoms**:
- 401 Unauthorized responses
- Invalid API key error messages

**Solution**:
1. **Verify API key**:
   - Check the key is correct (no extra spaces or characters)
   - Verify the key hasn't expired
   - Confirm the key has the necessary permissions

2. **Regenerate API key** if needed:
   - OpenAI: https://platform.openai.com/account/api-keys
   - Anthropic: https://console.anthropic.com/account/keys

3. **Check key scope**:
   - Ensure the key has permission for the models you're using
   - For organization-specific keys, verify organization settings

### "Rate limit exceeded"

**Cause**: Too many requests to the API in a short time period.

**Symptoms**:
- 429 Too Many Requests responses
- Requests failing intermittently

**Solution**:
1. **Implement backoff strategy**:
   - The provider already implements exponential backoff with 3 retries
   - Max delay is 100ms base + jitter

2. **Reduce request rate**:
   - Batch requests when possible
   - Implement request queuing
   - Increase time between requests

3. **Check usage limits**:
   - OpenAI: https://platform.openai.com/account/rate-limits
   - Verify your plan allows the request rate

4. **Contact provider support** if limits are too restrictive

## Structured Output Issues

### "Empty content" validation error

**Cause**: LLM returned an empty response when structured output was expected.

**Symptoms**:
- Validation fails with "empty content" message
- Response is null or whitespace

**Solution**:
1. **Increase max tokens**:
```csharp
var request = new LlmCompletionRequest
{
    Messages = new[] { LlmMessage.User("Generate output") },
    StructuredOutputSchema = schema,
    Options = new LlmProviderOptions
    {
        MaxTokens = 4000  // Increase if needed
    }
};
```

2. **Improve prompt clarity**:
   - Add explicit instruction to generate output
   - Provide examples in the prompt
   - Specify the exact format expected

3. **Check model capacity**:
   - Ensure the model can handle the request
   - Try a more capable model (e.g., gpt-4 instead of gpt-3.5-turbo)

### "Not valid JSON" validation error

**Cause**: LLM returned text that is not valid JSON.

**Symptoms**:
- Validation fails with "not valid JSON" message
- Response contains markdown formatting or extra text

**Solution**:
1. **Add JSON format requirement to prompt**:
```csharp
var messages = new[]
{
    LlmMessage.System("You must respond with ONLY valid JSON, no markdown or extra text."),
    LlmMessage.User("Generate a fix plan: " + userInput)
};
```

2. **Use response format hint**:
```csharp
var request = new LlmCompletionRequest
{
    Messages = messages,
    StructuredOutputSchema = schema,
    Options = new LlmProviderOptions
    {
        ResponseFormat = new { type = "json_object" }
    }
};
```

3. **Simplify the schema**:
   - Complex schemas may confuse the model
   - Break into smaller, simpler schemas if possible

### "Failed schema validation" error

**Cause**: Response is valid JSON but doesn't match the schema.

**Symptoms**:
- Validation fails with "failed schema validation" message
- Response structure doesn't match expected schema

**Solution**:
1. **Review schema constraints**:
   - Check `required` fields are all present
   - Verify `additionalProperties: false` isn't rejecting valid fields
   - Ensure field types match (string vs number, etc.)

2. **Add schema to prompt**:
```csharp
var schemaDescription = """
The response must be a JSON object with:
- "fixes": array of fix objects
- Each fix object must have:
  - "issueId": string
  - "description": string
  - "proposedChanges": array of change objects
""";

var messages = new[]
{
    LlmMessage.System("You are a fix planning assistant."),
    LlmMessage.User(schemaDescription + "\n\nGenerate fixes for: " + userInput)
};
```

3. **Adjust prompt for model behavior**:
   - Provide examples of expected output
   - Clarify ambiguous requirements
   - Reduce complexity if model struggles

4. **Disable strict validation** for non-critical workflows:
```csharp
var schema = LlmStructuredOutputSchema.FromJson(
    "my_schema",
    schemaJson,
    strictValidation: false);
```

### "Required property missing" error

**Cause**: Response is missing a required field.

**Solution**:
1. **Add field to prompt instructions**:
```csharp
var prompt = """
Generate a fix plan with the following required fields:
- issueId (required): unique identifier for the issue
- description (required): detailed description of the fix
- proposedChanges (required): array of proposed changes
""";
```

2. **Provide examples**:
```csharp
var examples = """
Example output:
{
  "issueId": "ISS-001",
  "description": "Fix authentication timeout",
  "proposedChanges": [...]
}
""";
```

3. **Check schema definition**:
   - Verify the field is actually required in the schema
   - Ensure the field name matches exactly (case-sensitive)

### "Additional properties not allowed" error

**Cause**: Response includes fields not defined in the schema with `additionalProperties: false`.

**Solution**:
1. **Update schema to allow additional properties**:
```json
{
  "type": "object",
  "properties": { ... },
  "required": [...],
  "additionalProperties": true
}
```

2. **Or add expected fields to schema**:
```json
{
  "type": "object",
  "properties": {
    "value": { "type": "string" },
    "metadata": { "type": "object" }
  },
  "required": ["value"],
  "additionalProperties": false
}
```

3. **Instruct model to not add extra fields**:
```csharp
var prompt = """
Generate output with ONLY these fields:
- issueId
- description
- proposedChanges

Do not add any other fields.
""";
```

## Tool Calling Issues

### Tool calls not being recognized

**Cause**: LLM not requesting tool calls or tools not properly registered.

**Solution**:
1. **Verify tools are registered**:
```csharp
var toolRegistry = kernel.GetRequiredService<IToolRegistry>();
var tools = toolRegistry.GetAll();
Assert.NotEmpty(tools);
```

2. **Check tool schema**:
```csharp
var toolDef = new LlmToolDefinition
{
    Name = "my_tool",
    Description = "Clear description of what the tool does",
    ParametersSchema = new
    {
        type = "object",
        properties = new
        {
            param1 = new { type = "string", description = "Parameter description" }
        },
        required = new[] { "param1" }
    }
};
```

3. **Instruct model to use tools**:
```csharp
var systemPrompt = """
You have access to the following tools:
- my_tool: [description]

Use these tools to help answer the user's question.
""";
```

### Tool execution fails

**Cause**: Tool implementation error or invalid parameters.

**Solution**:
1. **Check tool logs**:
   - Enable debug logging to see tool execution details
   - Check for exceptions in tool implementation

2. **Verify parameters**:
   - Ensure parameters match the schema
   - Check parameter types and ranges
   - Validate parameter values

3. **Test tool directly**:
```csharp
var tool = toolRegistry.Get("my_tool");
var result = await tool.InvokeAsync(
    new ToolRequest { Parameters = new { param1 = "test" } },
    context);
Assert.True(result.IsSuccess);
```

## Performance Issues

### Slow response times

**Cause**: Network latency, model processing time, or validation overhead.

**Solution**:
1. **Check network latency**:
   - Measure round-trip time to API endpoint
   - Consider using a closer region if available

2. **Optimize prompts**:
   - Reduce prompt length
   - Remove unnecessary context
   - Use concise instructions

3. **Reduce max tokens**:
```csharp
var options = new LlmProviderOptions
{
    MaxTokens = 1000  // Reduce if possible
};
```

4. **Use faster models**:
   - `gpt-3.5-turbo` is faster than `gpt-4`
   - Ollama local models are faster than cloud APIs

### High token usage

**Cause**: Verbose prompts or large context.

**Solution**:
1. **Optimize prompts**:
   - Remove redundant information
   - Use concise language
   - Summarize context instead of including full text

2. **Reduce context window**:
   - Keep conversation history shorter
   - Archive old messages
   - Summarize previous exchanges

3. **Monitor usage**:
```csharp
var response = await provider.CompleteAsync(request);
Console.WriteLine($"Tokens used: {response.Usage?.TotalTokens}");
```

## Logging and Debugging

### Enable debug logging

```csharp
// In Program.cs
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Debug);
});
```

### Check correlation IDs

All LLM requests include a correlation ID for tracing:

```csharp
var provider = kernel.GetRequiredService<ILlmProvider>();
// Correlation ID is automatically included in logs
```

### Common log patterns

- `"Failed to complete chat"` - Network or API error
- `"failed schema validation"` - Structured output validation failed
- `"Retrying request"` - Transient error, will retry
- `"Token usage"` - Usage metrics for monitoring

## Getting Help

If you encounter an issue not covered here:

1. **Check the logs** - Look for error messages and stack traces
2. **Verify configuration** - Ensure all required settings are present
3. **Test connectivity** - Verify network access to the API
4. **Review provider docs** - Check provider-specific limitations
5. **Contact support** - Reach out to the provider's support team

For issues specific to GMSD:
- Check the [Gmsd.Agents README](./README.md) for configuration examples
- Review the [Provider Expansion Guide](./PROVIDER_EXPANSION.md) for adding new providers
- Check test files for usage examples
