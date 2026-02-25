# LLM Provider Integration Guide

## Overview

This document provides provider-specific guidance for integrating LLM structured output schemas with GMSD planners and verifiers.

## Structured Output Schema Passing

LLM-generated artifacts (Phase Planner, Fix Planner, UAT Verifier) use strict schema validation via the `LlmStructuredOutputSchema` contract. The schema is passed to the LLM provider using provider-native structured output support.

### Schema Structure

```csharp
public sealed record LlmStructuredOutputSchema
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required JsonNode Schema { get; init; }
    public bool StrictValidation { get; init; } = true;
}
```

## OpenAI Integration

### Configuration

OpenAI supports structured output via the `response_format` parameter with `type: "json_schema"`.

**Supported Models:**
- `gpt-4-turbo` (2024-04-09 and later)
- `gpt-4o` (2024-08-06 and later)
- `gpt-4o-mini` (2024-07-18 and later)

### Implementation

The `SemanticKernelLlmProvider` automatically converts `LlmStructuredOutputSchema` to OpenAI's response format:

```csharp
var schema = LlmStructuredOutputSchema.FromJson(
    name: "phase_plan_v1",
    schemaJson: schemaJson,
    description: "GMSD phase planning schema");

var request = new LlmCompletionRequest
{
    Messages = messages,
    StructuredOutputSchema = schema,
    Options = new LlmProviderOptions { Temperature = 0.2f }
};

var response = await provider.CompleteAsync(request);
```

The provider converts this to:

```json
{
    "type": "json_schema",
    "json_schema": {
        "name": "phase_plan_v1",
        "description": "GMSD phase planning schema",
        "schema": { /* JSON Schema */ }
    }
}
```

### Validation

- OpenAI validates the response against the schema before returning
- If validation fails, the provider throws `LlmProviderException` with message containing "failed schema"
- The GMSD retry handler catches this and retries up to 3 times with enhanced prompts

### Best Practices

1. **Schema Naming:** Use lowercase with underscores (e.g., `phase_plan_v1`, `fix_plan_v1`)
2. **Schema Size:** Keep schemas under 100KB for optimal performance
3. **Required Fields:** Explicitly mark all required fields in the schema
4. **Temperature:** Use lower temperatures (0.1-0.3) for structured output to improve compliance
5. **Max Tokens:** Set appropriate limits based on expected output size

### Example Request

```csharp
var systemPrompt = @"You are a task planning assistant.
Generate a phase plan in JSON format matching the provided schema.
Ensure all required fields are present and have correct types.";

var userPrompt = "Break down this phase into 2-3 atomic tasks...";

var request = new LlmCompletionRequest
{
    Messages = new[]
    {
        LlmMessage.System(systemPrompt),
        LlmMessage.User(userPrompt)
    },
    StructuredOutputSchema = phasePlanSchema,
    Options = new LlmProviderOptions
    {
        Temperature = 0.2f,
        MaxTokens = 4000
    }
};

var response = await provider.CompleteAsync(request);
```

## Anthropic Integration

### Configuration

Anthropic supports structured output via the `thinking` and `json_mode` parameters (Claude 3.5 Sonnet and later).

**Supported Models:**
- `claude-3-5-sonnet-20241022` and later

### Implementation

Anthropic requires explicit JSON mode configuration. When using `LlmStructuredOutputSchema`, the provider should:

1. Include the schema in the system prompt
2. Request JSON output explicitly
3. Validate the response against the schema

```csharp
var systemPrompt = $@"You are a task planning assistant.
Generate a phase plan in JSON format.

Expected JSON Schema:
{schema.Schema.ToJsonString()}

Constraints:
- Response MUST be valid JSON
- All required fields MUST be present
- Field types MUST match the schema";

var request = new LlmCompletionRequest
{
    Messages = new[]
    {
        LlmMessage.System(systemPrompt),
        LlmMessage.User(userPrompt)
    },
    StructuredOutputSchema = schema,
    Options = new LlmProviderOptions
    {
        Temperature = 0.2f,
        MaxTokens = 4000
    }
};
```

### Validation

- Anthropic does not validate against the schema server-side
- GMSD validates the response using `JsonSchema.Net` after receiving it
- If validation fails, the retry handler retries with enhanced prompts

### Best Practices

1. **Schema in Prompt:** Always include the full schema in the system prompt
2. **Explicit Instructions:** Add explicit JSON formatting instructions
3. **Examples:** Include example JSON in the prompt for complex schemas
4. **Temperature:** Use lower temperatures (0.1-0.3) for structured output
5. **Validation:** Always validate responses client-side

### Example Request

```csharp
var schemaJson = schema.Schema.ToJsonString();
var systemPrompt = $@"You are a task planning assistant.
Generate a phase plan in JSON format matching this schema:

{schemaJson}

Requirements:
- Response must be valid JSON
- All required fields must be present
- Follow the schema structure exactly";

var request = new LlmCompletionRequest
{
    Messages = new[]
    {
        LlmMessage.System(systemPrompt),
        LlmMessage.User(userPrompt)
    },
    StructuredOutputSchema = schema,
    Options = new LlmProviderOptions
    {
        Temperature = 0.2f,
        MaxTokens = 4000
    }
};

var response = await provider.CompleteAsync(request);
// Response is validated client-side by SemanticKernelLlmProvider
```

## Retry Logic

Both providers use the same retry mechanism:

1. **Initial Request:** Send request with structured schema
2. **Validation Failure:** If response fails schema validation
3. **Retry with Enhancement:** Retry up to 3 times with enhanced system prompt
4. **Exponential Backoff:** Wait 1s, 2s, 4s between retries
5. **Diagnostic Creation:** On final failure, create diagnostic artifact with repair suggestions

### Retry Enhancement

Each retry adds clarification to the system prompt:

```
[Retry attempt N] Ensure your response is VALID JSON that matches the provided schema exactly. 
Check that all required fields are present and have the correct types.
```

## Diagnostic Artifacts

When LLM validation fails after retries, a diagnostic artifact is created:

**Path:** `.aos/diagnostics/{phase}/{artifact-id}.diagnostic.json`

**Example:**
```json
{
  "schemaVersion": 1,
  "schemaId": "gmsd:aos:schema:diagnostic:v1",
  "artifactPath": ".aos/spec/phases/PHASE-001/plan.json",
  "failedSchemaId": "gmsd:aos:schema:phase-plan:v1",
  "failedSchemaVersion": 1,
  "timestamp": "2026-02-19T18:07:00Z",
  "phase": "phase-planning",
  "context": {
    "phaseId": "PHASE-001",
    "planId": "PLAN-20260219-abc12345"
  },
  "validationErrors": [
    {
      "path": "$.tasks[0].fileScopes",
      "message": "fileScopes must be non-empty array",
      "expected": "array with at least 1 item",
      "actual": "empty array"
    }
  ],
  "repairSuggestions": [
    "Ensure all tasks have at least one file scope",
    "Verify fileScopes array is not empty",
    "Check that each fileScope has required fields: path, scopeType"
  ]
}
```

## Testing

### Unit Tests

Test structured output validation with mocked LLM responses:

```csharp
[Fact]
public async Task CompleteAsync_WithValidStructuredOutput_PassesValidation()
{
    var schema = LlmStructuredOutputSchema.FromJson("test_schema", schemaJson);
    var validOutput = """{ "field": "value" }""";
    
    var request = new LlmCompletionRequest
    {
        Messages = new[] { LlmMessage.User("Test") },
        StructuredOutputSchema = schema
    };
    
    // Mock provider to return valid output
    var result = await provider.CompleteAsync(request);
    
    result.Should().NotBeNull();
    result.Message.Content.Should().Contain("value");
}
```

### Integration Tests

Test end-to-end with real LLM providers:

```csharp
[Fact]
public async Task PhasePlanner_GeneratesPlanWithValidSchema()
{
    var brief = new PhaseBrief { /* ... */ };
    var plan = await planner.CreateTaskPlanAsync(brief, "RUN-001");
    
    plan.Should().NotBeNull();
    plan.Tasks.Should().NotBeEmpty();
    plan.Tasks.All(t => !string.IsNullOrEmpty(t.Title)).Should().BeTrue();
}
```

## Troubleshooting

### OpenAI: "Invalid schema"

**Cause:** Schema doesn't conform to OpenAI's JSON Schema subset

**Solution:**
- Validate schema with OpenAI's schema validator
- Remove unsupported keywords (e.g., `$comment`, `examples`)
- Ensure all property types are explicitly defined

### Anthropic: "Response doesn't match schema"

**Cause:** Model didn't follow the schema in the prompt

**Solution:**
- Add more explicit examples in the prompt
- Simplify the schema structure
- Use lower temperature (0.1-0.2)
- Add validation instructions to system prompt

### Retry Exhaustion

**Cause:** LLM consistently fails to produce valid schema-compliant output

**Solution:**
- Review the schema for clarity
- Simplify the schema structure
- Add examples to the prompt
- Check model capabilities (some models have better structured output support)
- Review diagnostic artifact for specific validation errors

## Provider Comparison

| Feature | OpenAI | Anthropic |
|---------|--------|-----------|
| Server-side Validation | Yes | No |
| Schema in Parameter | Yes | No (prompt only) |
| Supported Models | GPT-4 Turbo, GPT-4o, GPT-4o Mini | Claude 3.5 Sonnet+ |
| Validation Latency | Lower (server-side) | Higher (client-side) |
| Error Messages | Detailed | Generic |
| Retry Handling | Automatic | Manual |

## Migration Guide

### Adding a New Provider

1. Implement `ILlmProvider` interface
2. Convert `LlmStructuredOutputSchema` to provider format
3. Validate responses against schema using `JsonSchema.Net`
4. Throw `LlmProviderException` on validation failure
5. Add provider-specific tests

### Example: New Provider Implementation

```csharp
public class NewProviderLlmProvider : ILlmProvider
{
    public async Task<LlmCompletionResponse> CompleteAsync(
        LlmCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        // Convert schema to provider format
        var schemaPayload = ConvertSchema(request.StructuredOutputSchema);
        
        // Call provider API
        var response = await CallProviderAsync(schemaPayload, request, cancellationToken);
        
        // Validate response if schema was specified
        if (request.StructuredOutputSchema?.StrictValidation == true)
        {
            ValidateResponse(response, request.StructuredOutputSchema);
        }
        
        return ToLlmCompletionResponse(response);
    }
    
    private void ValidateResponse(
        ProviderResponse response,
        LlmStructuredOutputSchema schema)
    {
        var compiled = schema.GetCompiledSchema();
        var result = compiled.Evaluate(JsonDocument.Parse(response.Content).RootElement);
        
        if (!result.IsValid)
        {
            throw new LlmProviderException(
                ProviderName,
                $"Response failed schema validation: {FormatErrors(result)}");
        }
    }
}
```
