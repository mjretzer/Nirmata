# Change: Adopt Microsoft Semantic Kernel for LLM Orchestration

## Why

The current custom LLM provider abstraction (`ILlmProvider`, custom message types, manual adapter implementations) requires ongoing maintenance for:
- Provider-specific SDK updates and breaking changes
- Tool calling normalization across OpenAI, Anthropic, Azure OpenAI, and Ollama
- Retry logic, telemetry, and observability hooks
- Function calling schema translation

Microsoft Semantic Kernel (SK) provides a vendor-neutral abstraction layer that handles these concerns out-of-the-box:
- Built-in provider connectors (OpenAI, Azure OpenAI, Ollama via community, etc.)
- Automatic function calling and tool invocation loop
- Pluggable retry strategies, logging, and telemetry
- Prompt template management with Liquid/T4 syntax
- Chat history management and serialization
- Streaming support for future use cases

Adopting SK reduces maintenance burden, improves reliability, and aligns with the project's philosophy of using "boring, proven patterns" rather than custom infrastructure.

## What Changes

- **BREAKING**: Replace custom `ILlmProvider` interface with Semantic Kernel's `IChatCompletionService`
- **BREAKING**: Replace custom message types (`LlmMessage`, `LlmCompletionRequest`, `LlmCompletionResponse`) with SK's `ChatMessageContent`, `ChatHistory`
- **BREAKING**: Replace custom tool call abstractions with SK's `KernelFunction`, `FunctionResult`, and auto-invocation
- Remove custom adapter implementations (`OpenAiLlmAdapter`, `AnthropicLlmAdapter`, `AzureOpenAiLlmAdapter`, `OllamaLlmAdapter`)
- Replace custom prompt template loader with SK's `IPromptTemplate` and embedded resource support
- Update DI registration to use SK's `IKernelBuilder` and `AddKernel()` extensions
- Update `nirmata.Agents.csproj` to reference `Microsoft.SemanticKernel` packages
- Update `PH-ENG-0010` in `roadmap.md` to reflect Semantic Kernel adoption
- Preserve evidence capture patterns via `IAosEvidenceWriter` integration with SK middleware/filters

## Impact

- **Affected specs**: `agents-llm-provider-abstraction`
- **Affected code**: `nirmata.Agents/Execution/ControlPlane/Llm/**`, `nirmata.Agents/Configuration/**`, workflow classes using LLM
- **Configuration changes**: `Agents:Llm:Provider` → SK kernel configuration via `Agents:SemanticKernel:Provider`
- **Migration**: Workflows using `ILlmProvider` will need to inject `IChatCompletionService` or `Kernel` instead
