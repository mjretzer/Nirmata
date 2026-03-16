# Design: Semantic Kernel Migration

## Context

The current `nirmata.Agents` layer implements a custom LLM abstraction (`ILlmProvider`, `LlmMessage`, etc.) with manual adapters for each provider (OpenAI, Anthropic, Azure, Ollama). This abstraction requires ongoing maintenance and lacks features like automatic function calling, retry policies, and streaming.

Microsoft Semantic Kernel (SK) provides a mature, actively maintained abstraction that handles:
- Multiple provider connectors
- Automatic function calling with loop management
- Built-in retry, logging, and telemetry
- Prompt template management
- Chat history and context management

## Goals / Non-Goals

**Goals:**
- Replace custom LLM abstractions with Semantic Kernel equivalents
- Maintain existing evidence capture patterns (via `IAosEvidenceWriter`)
- Support the same four providers (OpenAI, Anthropic, Azure OpenAI, Ollama)
- Preserve configuration-driven provider selection

**Non-Goals:**
- Add new LLM features (streaming, multi-modal) in this change
- Migrate workflows to use SK plugins/agents architecture (future work)
- Replace the tool system (tools remain `ITool` registered with SK `Kernel`)

## Decisions

### Decision: Use `IChatCompletionService` as primary interface

**What and why:**
Workflows will inject `IChatCompletionService` for chat completions. This is SK's primary abstraction for LLM interactions and supports auto-function-calling via `AutoFunctionInvocationOptions`.

**Alternatives considered:**
- Inject `Kernel` directly — gives access to plugins and more, but adds coupling
- Keep custom `ILlmProvider` wrapper around SK — adds unnecessary abstraction layer

### Decision: Use SK's `KernelFunction` for tool invocation

**What and why:**
Tools will be registered as `KernelFunction` instances in the SK `Kernel`. SK's auto-invocation will handle the tool-calling loop.

**Migration path:**
- Create adapter to wrap existing `ITool` implementations as `KernelFunction`
- Register tools via `Kernel.Plugins.AddFromFunctions()`

### Decision: Evidence capture via custom `ILogger` or filter

**What and why:**
SK provides `FunctionInvocationFilter` and logging hooks. We'll implement a custom filter that writes `LlmCallEnvelope` evidence via `IAosEvidenceWriter`.

**Alternatives considered:**
- Wrap `IChatCompletionService` — adds complexity
- Use SK's built-in telemetry — doesn't integrate with AOS evidence store

### Decision: Prompt templates via SK's `PromptTemplateConfig`

**What and why:**
Replace custom `IPromptTemplateLoader` with SK's prompt template system, loading from embedded resources.

**Migration path:**
- Convert `.prompt.txt` files to SK-compatible format
- Use `KernelFunctionFactory.CreateFromPrompt()` with embedded resource streaming

### Decision: Configuration mapping

**Current:** `Agents:Llm:Provider`, `Agents:Llm:OpenAi:ApiKey`, etc.
**New:** `Agents:SemanticKernel:Provider` with provider-specific subsections mapped to SK connector options.

**Mapping:**
- `openai` → `AddOpenAIChatCompletion()`
- `azure-openai` → `AddAzureOpenAIChatCompletion()`
- `ollama` → `AddOllamaChatCompletion()` (via community package)
- `anthropic` → Custom connector or direct API until official SK support

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| Anthropic not officially supported in SK | Use community connector or maintain minimal Anthropic adapter that translates to SK types |
| Breaking change for all workflows | Staged migration: add SK alongside, migrate workflows one-by-one, remove custom abstractions after |
| Evidence format changes | Ensure `LlmCallEnvelope` schema remains compatible; only implementation changes |
| Package size increase | SK adds ~2-3MB; acceptable for the functionality gained |

## Migration Plan

### Phase 1: Infrastructure (this change)
1. Add Semantic Kernel packages to `nirmata.Agents.csproj`
2. Create `SemanticKernelServiceCollectionExtensions` with `AddSemanticKernel()`
3. Implement `ITool` → `KernelFunction` adapter
4. Implement evidence capture filter
5. Create provider configuration mapping

### Phase 2: Provider Adapters (follow-up)
1. Remove custom adapters
2. Test each provider with SK connectors
3. Handle Anthropic if needed via custom connector

### Phase 3: Workflow Migration (follow-up)
1. Migrate `NewProjectInterviewer` to use `IChatCompletionService`
2. Migrate `PhasePlanner` workflows
3. Migrate remaining workflows
4. Remove custom `ILlmProvider` and related types

## Open Questions

- Should we use SK's `IChatCompletionConnector` for Anthropic, or wait for official support?
- Do we want to adopt SK's plugin architecture in the future for tool organization?
- Should we use SK's built-in retry policies or maintain our own?
