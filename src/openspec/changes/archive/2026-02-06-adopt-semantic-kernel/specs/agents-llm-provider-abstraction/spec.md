## MODIFIED Requirements

### Requirement: Vendor-neutral LLM provider interface is defined

The system SHALL define LLM provider contracts using Microsoft Semantic Kernel abstractions as the primary interface for LLM operations.

The DI container MUST register `IChatCompletionService` as the primary interface for chat completions, configured via `Kernel` with provider-specific connectors.

#### Scenario: Completion request returns normalized response via Semantic Kernel
- **GIVEN** Semantic Kernel is configured with a provider connector
- **WHEN** `IChatCompletionService.GetChatMessageContentsAsync()` is called with a valid `ChatHistory`
- **THEN** the returned `ChatMessageContent` contains normalized message content, role, and optional function calls

#### Scenario: Provider is selected via SK kernel configuration
- **GIVEN** configuration contains `Agents:SemanticKernel:Provider = "openai"` and appropriate API key settings
- **WHEN** `AddSemanticKernel()` is called during service registration
- **THEN** the kernel is configured with the specified provider's chat completion connector

### Requirement: LLM message types are normalized

The system SHALL use Semantic Kernel's native message types for vendor-neutral message representation.

`ChatMessageContent` is used for individual messages with:
- `AuthorRole` (`System`, `User`, `Assistant`, `Tool`) — message role
- `Content` — message text content
- `Items` — contains `FunctionCallContent` for tool calls when present
- `Metadata` — additional provider-specific metadata

`ChatHistory` is used for conversation context management, replacing custom `LlmCompletionRequest.Messages`.

`PromptExecutionSettings` (or provider-specific subclasses like `OpenAIPromptExecutionSettings`) is used for temperature, max tokens, and model selection.

#### Scenario: Tool call request is represented in SK format
- **GIVEN** an assistant message requesting tool invocations
- **WHEN** represented as `ChatMessageContent`
- **THEN** `AuthorRole` is `Assistant`, `Content` may be null, and `Items` contains `FunctionCallContent` entries

#### Scenario: Tool result is represented in SK format
- **GIVEN** a tool execution result to send back to the LLM
- **WHEN** represented as `ChatMessageContent`
- **THEN** `AuthorRole` is `Tool`, `Content` contains the stringified result, and metadata correlates to the original call

### Requirement: Tool-call representation is normalized across providers

The system SHALL use Semantic Kernel's `KernelFunction` and `FunctionResult` types for consistent tool-handling across LLM providers.

Tools are registered as `KernelFunction` instances in a `KernelPlugin` within the SK `Kernel`.

SK's `AutoFunctionInvocationOptions` enables automatic function calling loop management.

`FunctionCallContent` represents tool calls from the assistant.
`FunctionResultContent` represents tool execution results.

#### Scenario: Tool is invoked via SK auto-function-calling
- **GIVEN** a `Kernel` configured with plugins containing `KernelFunction` instances
- **WHEN** `GetChatMessageContentsAsync()` is called with `AutoFunctionInvocationOptions` enabled
- **THEN** SK automatically invokes the appropriate `KernelFunction` and returns the complete response

#### Scenario: Tool definition is registered with the kernel
- **GIVEN** an existing `ITool` implementation
- **WHEN** it is registered with the kernel via the tool adapter
- **THEN** it is available as a `KernelFunction` with proper JSON schema derived from the tool's contract

### Requirement: Provider adapters translate between native and normalized formats

The system SHALL use Semantic Kernel's built-in provider connectors rather than custom adapters.

Supported providers and their SK connectors:
- OpenAI → `Microsoft.SemanticKernel.Connectors.OpenAI`
- Azure OpenAI → `Microsoft.SemanticKernel.Connectors.AzureOpenAI`
- Ollama → `Microsoft.SemanticKernel.Connectors.Ollama` (community) or custom connector
- Anthropic → Custom connector implementing `IChatCompletionService` until official SK support

Custom connector for Anthropic (if needed) MUST:
- Implement `IChatCompletionService` interface
- Translate `ChatHistory` to Anthropic's message format
- Map `FunctionCallContent` to/from Anthropic's tool-use/tool-result format
- Handle Anthropic-specific options (e.g., `ClaudePromptExecutionSettings`)

#### Scenario: OpenAI connector sends chat completion request
- **GIVEN** a `ChatHistory` with messages and `OpenAIPromptExecutionSettings`
- **WHEN** `GetChatMessageContentsAsync` is invoked on the OpenAI chat completion service
- **THEN** SK calls OpenAI Chat Completions API with correctly mapped messages, model, temperature, and max tokens

#### Scenario: Azure OpenAI connector handles tool-use responses
- **GIVEN** an Azure OpenAI response containing `tool_calls`
- **WHEN** SK processes the response
- **THEN** `ChatMessageContent.Items` contains `FunctionCallContent` entries representing the tool calls

### Requirement: Provider is selected via configuration and DI

The system SHALL support configuration-driven selection of the LLM provider via Semantic Kernel's DI extensions.

The DI extension method `AddSemanticKernel(IServiceCollection, IConfiguration)` MUST:
- Read `Agents:SemanticKernel:Provider` configuration value
- Configure the appropriate SK connector based on the value
- Register `Kernel` and `IChatCompletionService` with the configured provider
- Support provider-specific configuration subsections (e.g., `Agents:SemanticKernel:OpenAi:ApiKey`)

Provider-specific extension methods SHOULD be available:
- `AddOpenAiChatCompletion(this IKernelBuilder, IConfiguration)`
- `AddAzureOpenAiChatCompletion(this IKernelBuilder, IConfiguration)`
- `AddOllamaChatCompletion(this IKernelBuilder, IConfiguration)`
- `AddAnthropicChatCompletion(this IKernelBuilder, IConfiguration)` (custom implementation)

#### Scenario: DI resolves OpenAI provider from configuration
- **GIVEN** configuration contains `Agents:SemanticKernel:Provider = "openai"` and `Agents:SemanticKernel:OpenAi:ApiKey`
- **WHEN** services are built and `IChatCompletionService` is resolved
- **THEN** an OpenAI chat completion service is returned configured with the specified API key

#### Scenario: Missing provider configuration throws exception
- **GIVEN** configuration does not contain `Agents:SemanticKernel:Provider`
- **WHEN** `AddSemanticKernel` is called during service registration
- **THEN** an `InvalidOperationException` is thrown with message indicating missing provider configuration

### Requirement: Prompt templates are loadable by ID

The system SHALL provide prompt templates via Semantic Kernel's prompt template system, loading from embedded resources.

Templates are loaded using `KernelFunctionFactory.CreateFromPrompt()` with content streamed from embedded resources.

Template IDs map to embedded resource paths in `nirmata.Agents.Resources.Prompts` with `.prompt.txt` or `.prompt.yaml` extensions.

SK's `PromptTemplateConfig` supports variable substitution and template syntax.

#### Scenario: Template is retrieved by ID and rendered
- **GIVEN** an embedded resource exists at `nirmata.Agents.Resources.Prompts.planning.task-breakdown.v1.prompt.txt`
- **WHEN** the template is loaded via `KernelFunctionFactory.CreateFromPrompt` with streaming resource
- **THEN** a `KernelFunction` is returned that renders the template with provided arguments

#### Scenario: Template with variables is rendered
- **GIVEN** a template containing `{{$taskName}}` and `{{$context}}` variables
- **WHEN** the template is invoked with `KernelArguments` containing those values
- **THEN** the rendered output contains the substituted variable values

### Requirement: LLM calls are recorded as auditable evidence

The system SHALL record all LLM provider invocations using a Semantic Kernel function filter that writes to `IAosEvidenceWriter`.

An `AosEvidenceFunctionFilter` implementing `IFunctionInvocationFilter` MUST:
- Capture pre-invocation state (request summary, timestamp)
- Capture post-invocation state (response summary, finish reason, usage)
- Write `LlmCallEnvelope` to the evidence store via `IAosEvidenceWriter`

The filter MUST be registered with SK's filter pipeline during kernel configuration.

#### Scenario: Evidence capture via SK function filter
- **GIVEN** a workflow invokes `IChatCompletionService.GetChatMessageContentsAsync`
- **WHEN** the call completes
- **THEN** an `LlmCallEnvelope` is written to `.aos/evidence/runs/<run-id>/logs/llm-<call-id>.json` via the registered filter

#### Scenario: Agent workflow records LLM call with SK
- **GIVEN** a workflow in nirmata.Agents executes a chat completion via SK
- **WHEN** the evidence filter processes the invocation
- **THEN** the envelope contains: provider identifier as string, model identifier as string, token usage (if available), and duration in milliseconds

## REMOVED Requirements

### Requirement: Custom ILlmProvider interface and implementations
**Reason**: Replaced by Semantic Kernel's `IChatCompletionService` abstraction
**Migration**: Update DI to resolve `IChatCompletionService` instead of `ILlmProvider`; update call sites to use `ChatHistory` instead of `LlmCompletionRequest`

### Requirement: Custom message types (LlmMessage, LlmCompletionRequest, LlmCompletionResponse)
**Reason**: Replaced by Semantic Kernel's `ChatMessageContent`, `ChatHistory`, and streaming support
**Migration**: Replace `LlmMessage` with `ChatMessageContent`; replace `LlmCompletionRequest.Messages` with `ChatHistory`; replace `LlmCompletionResponse.Message` with returned `ChatMessageContent`

### Requirement: Custom tool call types (LlmToolCall, LlmToolResult, LlmToolDefinition)
**Reason**: Replaced by Semantic Kernel's `FunctionCallContent`, `FunctionResultContent`, and `KernelFunction`
**Migration**: Tools registered as `KernelFunction` in `Kernel`; tool calls represented as `FunctionCallContent` in message items

### Requirement: Custom provider adapters (OpenAiLlmAdapter, AnthropicLlmAdapter, AzureOpenAiLlmAdapter, OllamaLlmAdapter)
**Reason**: Replaced by Semantic Kernel's built-in connectors
**Migration**: Remove adapter classes; configure SK connectors via `AddOpenAIChatCompletion`, `AddAzureOpenAIChatCompletion`, etc.

### Requirement: IPromptTemplateLoader interface and EmbeddedResourcePromptLoader
**Reason**: Replaced by Semantic Kernel's prompt loading via `KernelFunctionFactory.CreateFromPrompt` and embedded resource streaming
**Migration**: Load prompts directly via SK APIs with embedded resource streams; no custom loader needed
