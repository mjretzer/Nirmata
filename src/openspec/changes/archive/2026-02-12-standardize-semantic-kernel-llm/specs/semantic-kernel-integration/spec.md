# semantic-kernel-integration Specification

## Purpose

Define requirements for standardizing Microsoft Semantic Kernel as the single production-grade LLM integration path, replacing the dual-path approach where custom `ILlmProvider` and SK infrastructure coexisted.

## ADDED Requirements

### Requirement: Semantic Kernel backs the ILlmProvider contract

The system SHALL provide an `ILlmProvider` implementation that delegates all LLM operations to Microsoft Semantic Kernel's `IChatCompletionService`.

This adapter MUST:
- Implement `ILlmProvider.CompleteAsync()` by translating `LlmCompletionRequest` to SK `ChatHistory` and `PromptExecutionSettings`
- Implement `ILlmProvider.StreamCompletionAsync()` using SK's streaming chat completion
- Return `LlmCompletionResponse` by translating SK `ChatMessageContent` to existing contract types
- Preserve evidence capture via existing `AosEvidenceFunctionFilter`

#### Scenario: Adapter translates request to SK format
- **GIVEN** an `LlmCompletionRequest` with messages and tool definitions
- **WHEN** `SemanticKernelLlmProvider.CompleteAsync()` is invoked
- **THEN** the adapter creates a `ChatHistory` with equivalent `ChatMessageContent` entries
- **AND** invokes `IChatCompletionService.GetChatMessageContentsAsync()`
- **AND** returns an `LlmCompletionResponse` with translated content

#### Scenario: Streaming completion yields deltas
- **GIVEN** a streaming request via `StreamCompletionAsync()`
- **WHEN** the adapter consumes `IChatCompletionService.GetStreamingChatMessageContentsAsync()`
- **THEN** each `StreamingChatMessageContent` is yielded as an `LlmDelta`
- **AND** streaming can be cancelled via `CancellationToken`

### Requirement: Custom provider adapters are removed

The system SHALL remove custom `ILlmProvider` adapter implementations for individual providers.

The following adapters MUST be removed:
- `OpenAiLlmAdapter`
- `AnthropicLlmAdapter`
- `AzureOpenAiLlmAdapter`
- `OllamaLlmAdapter`

SK connectors MUST handle provider-specific translation:
- `Microsoft.SemanticKernel.Connectors.OpenAI` for OpenAI
- `Microsoft.SemanticKernel.Connectors.AzureOpenAI` for Azure OpenAI
- `Microsoft.SemanticKernel.Connectors.Ollama` for Ollama
- Custom `AnthropicChatCompletionService` (already implemented) for Anthropic

#### Scenario: OpenAI request uses SK connector
- **GIVEN** configuration specifies OpenAI provider
- **WHEN** a completion is requested via `ILlmProvider`
- **THEN** the SK OpenAI connector handles API communication
- **AND** no custom `OpenAiLlmAdapter` code is invoked

#### Scenario: Anthropic request uses custom SK connector
- **GIVEN** configuration specifies Anthropic provider
- **WHEN** a completion is requested via `ILlmProvider`
- **THEN** the custom `AnthropicChatCompletionService` (implementing SK's `IChatCompletionService`) handles the request
- **AND** no custom `AnthropicLlmAdapter` code is invoked

### Requirement: DI registration uses Semantic Kernel

The system SHALL update dependency injection registration to use Semantic Kernel as the LLM implementation.

The `AddLlmProvider()` extension method MUST:
- Call `AddSemanticKernel()` to register SK services
- Register `SemanticKernelLlmProvider` as the `ILlmProvider` implementation
- Maintain backward compatibility with legacy configuration keys during transition

#### Scenario: Services resolve SK-backed provider
- **GIVEN** `services.AddLlmProvider(configuration)` is called during startup
- **WHEN** `ILlmProvider` is resolved from the service provider
- **THEN** a `SemanticKernelLlmProvider` instance is returned
- **AND** the provider delegates to SK's `IChatCompletionService`

#### Scenario: Configuration backward compatibility
- **GIVEN** configuration uses legacy `Agents:Llm:Provider` key
- **WHEN** `AddLlmProvider()` reads configuration
- **THEN** the provider is correctly resolved with a deprecation warning
- **AND** the service functions identically to new configuration path

### Requirement: ILlmProvider contract is deprecated

The system SHALL mark the `ILlmProvider` abstraction and related types as obsolete to signal the migration path.

The following types MUST be marked with `[Obsolete]`:
- `ILlmProvider`
- `LlmCompletionRequest`
- `LlmCompletionResponse`
- `LlmMessage`
- `LlmDelta`
- `LlmProviderOptions`
- `LlmToolCall`
- `LlmToolDefinition`

The obsolete message SHOULD direct developers to use `Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService`.

#### Scenario: Compiler warns on ILlmProvider usage
- **GIVEN** code references `ILlmProvider` after this change
- **WHEN** the project is compiled
- **THEN** a compiler warning (CS0618) is emitted
- **AND** the warning message indicates the preferred alternative (`IChatCompletionService`)

#### Scenario: Existing workflows continue to function
- **GIVEN** existing workflow classes inject `ILlmProvider`
- **WHEN** the system runs after this change
- **THEN** workflows continue to function without code changes (backward compatibility)
- **AND** deprecation warnings appear at compile time only

### Requirement: Configuration schema unifies under SemanticKernel section

The system SHALL unify LLM configuration under the `Agents:SemanticKernel` section.

The new configuration schema MUST support:
- `Agents:SemanticKernel:Provider` — provider identifier (`openai`, `azureopenai`, `ollama`, `anthropic`)
- `Agents:SemanticKernel:OpenAi:*` — OpenAI-specific settings
- `Agents:SemanticKernel:AzureOpenAi:*` — Azure OpenAI-specific settings
- `Agents:SemanticKernel:Ollama:*` — Ollama-specific settings
- `Agents:SemanticKernel:Anthropic:*` — Anthropic-specific settings

Legacy configuration (`Agents:Llm:*`) MUST be supported with a deprecation warning for one release cycle.

#### Scenario: New configuration schema is valid
- **GIVEN** configuration uses `Agents:SemanticKernel:Provider = "openai"`
- **WHEN** the application starts
- **THEN** the OpenAI connector is configured with settings from `Agents:SemanticKernel:OpenAi`
- **AND** no deprecation warning is logged

#### Scenario: Legacy configuration triggers warning
- **GIVEN** configuration uses `Agents:Llm:Provider = "openai"`
- **WHEN** the application starts
- **THEN** the provider is correctly configured
- **AND** a warning is logged: "Legacy configuration detected. Migrate to Agents:SemanticKernel:*"

### Requirement: Tool calling integrates with SK Kernel

The system SHALL ensure tool calling works through SK's `Kernel` and `KernelFunction` system.

The existing `ITool` to `KernelFunction` adapter MUST remain functional:
- `ToolToKernelFunctionAdapter` wraps `ITool` as `KernelFunction`
- `KernelPluginFactory` creates SK plugins from the tool registry
- `AutoFunctionInvocationOptions` enables automatic tool calling

#### Scenario: Tool call flows through SK
- **GIVEN** a request with available tools in the registry
- **WHEN** the LLM requests a tool invocation
- **THEN** SK's auto-invocation handles the tool call
- **AND** results are returned to the LLM via SK's function result system
- **AND** evidence is captured via `AosEvidenceFunctionFilter`

#### Scenario: Evidence captures SK tool calls
- **GIVEN** a tool is invoked during LLM completion
- **WHEN** `IFunctionInvocationFilter` intercepts the call
- **THEN** `AosEvidenceFunctionFilter` writes evidence to `.aos/evidence/`
- **AND** the envelope contains SK-specific metadata (function name, plugin name)
