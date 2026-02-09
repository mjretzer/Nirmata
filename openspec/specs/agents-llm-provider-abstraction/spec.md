# agents-llm-provider-abstraction Specification

## Purpose
TBD - created by archiving change add-llm-contracts-adapters. Update Purpose after archive.
## Requirements
### Requirement: Vendor-neutral LLM provider interface is defined
The system SHALL define an `ILlmProvider` interface as the primary contract for LLM operations.

The interface MUST specify:
- `Task<LlmCompletionResponse> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken)` — sends a completion request and returns the LLM response
- `string ProviderId { get; }` — stable identifier for the provider (e.g., `openai`, `anthropic`)

#### Scenario: Completion request returns normalized response
- **GIVEN** an `ILlmProvider` implementation is configured
- **WHEN** `CompleteAsync` is called with a valid `LlmCompletionRequest`
- **THEN** the returned `LlmCompletionResponse` contains normalized message content, role, and optional tool calls

#### Scenario: Provider identifier is stable
- **GIVEN** an `ILlmProvider` implementation
- **WHEN** the `ProviderId` property is accessed
- **THEN** it returns a stable, lowercase, kebab-case identifier (e.g., `openai`, `azure-openai`, `anthropic`, `ollama`)

### Requirement: LLM message types are normalized
The system SHALL define `LlmMessage`, `LlmCompletionRequest`, and `LlmCompletionResponse` types for vendor-neutral message representation.

`LlmMessage` MUST include:
- `MessageRole Role` — enum values: `System`, `User`, `Assistant`, `Tool`
- `string Content` — message text content
- `IReadOnlyList<LlmToolCall>? ToolCalls` — tool calls requested by assistant (null if none)
- `string? ToolCallId` — identifier for tool result messages correlating to a prior tool call

`LlmCompletionRequest` MUST include:
- `IReadOnlyList<LlmMessage> Messages` — conversation history
- `IReadOnlyList<LlmToolDefinition>? Tools` — available tools (null if no tool calling)
- `LlmProviderOptions Options` — temperature, max tokens, model selection

`LlmCompletionResponse` MUST include:
- `LlmMessage Message` — the assistant's response message
- `LlmUsage? Usage` — token usage statistics (prompt, completion, total)
- `string? FinishReason` — reason for completion (e.g., `stop`, `length`, `tool_calls`)

#### Scenario: Tool call request is represented
- **GIVEN** an assistant message requesting tool invocations
- **WHEN** represented as `LlmMessage`
- **THEN** `Role` is `Assistant`, `Content` may be null, and `ToolCalls` contains one or more `LlmToolCall` entries

#### Scenario: Tool result is represented
- **GIVEN** a tool execution result to send back to the LLM
- **WHEN** represented as `LlmMessage`
- **THEN** `Role` is `Tool`, `Content` contains the stringified result, and `ToolCallId` matches the original call

### Requirement: Tool-call representation is normalized across providers
The system SHALL define `LlmToolCall`, `LlmToolResult`, and `LlmToolDefinition` types for consistent tool-handling across LLM providers.

`LlmToolCall` MUST include:
- `string Id` — unique identifier for this call instance
- `string Name` — the tool/function name being called
- `string ArgumentsJson` — arguments as JSON string (to be parsed by adapter)

`LlmToolResult` MUST include:
- `string ToolCallId` — identifier matching the original `LlmToolCall.Id`
- `bool IsSuccess` — whether the tool execution succeeded
- `string Content` — result content (or error message if failed)

`LlmToolDefinition` MUST include:
- `string Name` — tool identifier used by the LLM
- `string Description` — human-readable purpose for the LLM
- `JsonElement InputSchema` — JSON Schema describing expected parameters

#### Scenario: Tool call is parsed from provider-native format
- **GIVEN** a provider response containing native tool-call format (e.g., OpenAI `tool_calls` array)
- **WHEN** the adapter translates to `LlmMessage`
- **THEN** `ToolCalls` contains normalized `LlmToolCall` entries with parsed `Id`, `Name`, and `ArgumentsJson`

#### Scenario: Tool definition is serialized to provider-native format
- **GIVEN** an `LlmToolDefinition` with name, description, and input schema
- **WHEN` the adapter serializes for provider request
- **THEN** the output matches the provider's expected tool specification format (e.g., OpenAI `functions` array)

### Requirement: Provider adapters translate between native and normalized formats
The system SHALL provide `ILlmProvider` implementations for each supported provider that translate requests/responses between normalized types and native SDK formats.

Adapters MUST be implemented for:
- OpenAI (`OpenAiLlmAdapter`)
- Anthropic (`AnthropicLlmAdapter`)
- Azure OpenAI (`AzureOpenAiLlmAdapter`)
- Ollama (`OllamaLlmAdapter`)

Each adapter MUST:
- Translate `LlmCompletionRequest` to provider-specific request format
- Translate provider response to `LlmCompletionResponse`
- Map `FinishReason` values to normalized strings
- Translate token usage to `LlmUsage` when available from provider

#### Scenario: OpenAI adapter sends chat completion request
- **GIVEN** an `LlmCompletionRequest` with messages and options
- **WHEN** `OpenAiLlmAdapter.CompleteAsync` is invoked
- **THEN** the adapter calls OpenAI Chat Completions API with correctly mapped messages, model, temperature, and max tokens

#### Scenario: Anthropic adapter handles tool-use responses
- **GIVEN** an Anthropic API response containing `stop_reason: tool_use` and tool use blocks
- **WHEN** `AnthropicLlmAdapter` processes the response
- **THEN** the returned `LlmCompletionResponse` has `FinishReason = "tool_calls"` and `Message.ToolCalls` contains normalized calls

### Requirement: Provider is selected via configuration and DI
The system SHALL support configuration-driven selection of the LLM provider via dependency injection.

The DI extension method `AddLlmProvider(IServiceCollection, IConfiguration)` MUST:
- Read `Agents:Llm:Provider` configuration value
- Register the appropriate `ILlmProvider` implementation based on the value
- Throw `InvalidOperationException` if provider is unspecified or unrecognized

Provider-specific extension methods MUST be available:
- `AddOpenAiLlm(this IServiceCollection, IConfiguration)`
- `AddAnthropicLlm(this IServiceCollection, IConfiguration)`
- `AddAzureOpenAiLlm(this IServiceCollection, IConfiguration)`
- `AddOllamaLlm(this IServiceCollection, IConfiguration)`

#### Scenario: DI resolves OpenAI provider from configuration
- **GIVEN** configuration contains `Agents:Llm:Provider = "openai"` and `Agents:Llm:OpenAi:ApiKey`
- **WHEN** services are built and `ILlmProvider` is resolved
- **THEN** an `OpenAiLlmAdapter` instance is returned configured with the specified API key

#### Scenario: Missing provider configuration throws exception
- **GIVEN** configuration does not contain `Agents:Llm:Provider`
- **WHEN** `AddLlmProvider` is called during service registration
- **THEN** an `InvalidOperationException` is thrown with message indicating missing provider configuration

### Requirement: Prompt templates are loadable by ID
The system SHALL provide an `IPromptTemplateLoader` interface for retrieving prompt content by identifier.

`IPromptTemplateLoader` MUST specify:
- `PromptTemplate? GetById(string id)` — retrieves template by ID, returns null if not found
- `bool Exists(string id)` — checks if template with given ID exists

`PromptTemplate` MUST include:
- `string Id` — stable identifier (e.g., `planning.task-breakdown.v1`)
- `string Content` — the prompt text content
- `IReadOnlyDictionary<string, string> Metadata` — optional tags, description, version

The `EmbeddedResourcePromptLoader` implementation MUST:
- Load templates from embedded resources in `Gmsd.Agents.Resources.Prompts`
- Support `.prompt.txt` and `.md` file extensions
- Use filename (without extension) as template ID

#### Scenario: Template is retrieved by ID
- **GIVEN** an embedded resource exists at `Gmsd.Agents.Resources.Prompts.planning.task-breakdown.v1.prompt.txt`
- **WHEN** `loader.GetById("planning.task-breakdown.v1")` is called
- **THEN** a `PromptTemplate` is returned with matching `Id` and file content as `Content`

#### Scenario: Missing template returns null
- **GIVEN** no template exists with ID `nonexistent.template`
- **WHEN** `loader.GetById("nonexistent.template")` is called
- **THEN** the method returns `null`

### Requirement: LLM calls are recorded as auditable evidence
The system SHALL record all LLM provider invocations using the existing call envelope infrastructure without Gmsd.Aos having a compile-time dependency on Gmsd.Agents.

**Migration notes:**
- **Previous location**: Gmsd.Aos/Engine/Evidence/LlmCallEnvelope.cs
- **New approach**: LlmCallEnvelope remains in Gmsd.Aos but operates on primitive types

Each LLM call MUST produce an `LlmCallEnvelope` record containing:
- `schemaVersion: 1`
- `runId` — correlation ID from execution context
- `callId` — unique identifier for this call
- `provider` — provider identifier as **string** (e.g., `openai`, `anthropic`), NOT `ILlmProvider.ProviderId` property access
- `model` — model identifier used as **string** (e.g., `gpt-4`, `claude-3-sonnet`)
- `status` — `succeeded` or `failed`
- `requestSummary` — truncated/summary of request (messages count, tools count)
- `responseSummary` — truncated/summary of response (finish reason, usage)
- `timestampUtc` — ISO 8601 timestamp
- `durationMs` — elapsed milliseconds

#### Scenario: Evidence capture without AOS→Agents dependency
- **GIVEN** a workflow in Gmsd.Agents calls an LLM provider
- **WHEN** the Agents layer records evidence via IAosEvidenceWriter
- **THEN** Gmsd.Aos captures the envelope using only string/primitive types
- **AND** Gmsd.Aos does not reference Gmsd.Agents.Contracts.Llm types

#### Scenario: Agent workflow records LLM call
- **GIVEN** a workflow in Gmsd.Agents executes an LLM completion via ILlmProvider
- **WHEN** the workflow calls IAosEvidenceWriter to record the interaction
- **THEN** the envelope is written to `.aos/evidence/runs/<run-id>/logs/llm-<call-id>.json` with all metadata as strings

