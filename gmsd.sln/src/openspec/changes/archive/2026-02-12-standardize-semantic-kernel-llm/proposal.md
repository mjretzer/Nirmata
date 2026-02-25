# Change: Standardize on Microsoft Semantic Kernel for LLM Integration

## Why

The GMSD codebase currently has **dual LLM integration paths** that create maintenance burden and confusion:

1. **Semantic Kernel infrastructure** (already scaffolded):
   - Packages installed (`Microsoft.SemanticKernel.*` v1.70.0)
   - `SemanticKernelServiceCollectionExtensions` with full provider support (OpenAI, Azure OpenAI, Ollama, Anthropic)
   - `IChatCompletionService` registered in DI
   - Evidence filters and tool adapters for SK

2. **Custom `ILlmProvider` abstraction** (actively used by workflows):
   - Custom message types (`LlmMessage`, `LlmCompletionRequest`, `LlmCompletionResponse`)
   - Manual provider adapters (`OpenAiLlmAdapter`, `AnthropicLlmAdapter`, etc.)
   - Used by `LlmChatResponder`, `PhasePlanner`, and other workflows

This split means:
- **Duplicate maintenance**: Two sets of provider connectors, retry logic, configuration
- **Unclear guidance**: New code doesn't know which abstraction to use
- **Incomplete SK migration**: SK infrastructure exists but workflows don't leverage it

Microsoft Semantic Kernel provides a production-grade, actively maintained abstraction that handles:
- Multiple provider connectors with consistent interfaces
- Automatic function calling and tool invocation loops
- Built-in retry policies, telemetry, and observability hooks
- Prompt template management
- Streaming support

Standardizing on SK as the single "blessed" LLM path eliminates the dual-path problem and aligns with the Remediation.md directive to "pick a single blessed LLM integration path and complete it end-to-end."

## What Changes

### Decision: Implement ILlmProvider adapter over IChatCompletionService

Rather than replacing `ILlmProvider` usage across all workflows immediately (high churn), we will:

1. **Create an `ILlmProvider` adapter** that wraps `IChatCompletionService`
   - Preserves existing workflow contracts
   - Delegates all LLM operations to SK's `IChatCompletionService`
   - Maintains existing evidence capture patterns

2. **Deprecate custom provider adapters**
   - Remove `OpenAiLlmAdapter`, `AnthropicLlmAdapter`, `AzureOpenAiLlmAdapter`, `OllamaLlmAdapter`
   - SK connectors handle provider-specific translation

3. **Update DI registration**
   - `AddLlmProvider()` will resolve the SK-backed adapter
   - Configuration maps to `SemanticKernelOptions`

4. **Mark custom abstractions as obsolete**
   - `ILlmProvider` remains but is marked `[Obsolete("Use IChatCompletionService directly")]`
   - New code should inject `IChatCompletionService` or `Kernel`
   - Future change can migrate workflows incrementally

### Scope

- **Gmsd.Agents/Execution/ControlPlane/Llm/**:
  - Add `SemanticKernelLlmProvider` adapter class
  - Remove manual provider adapters
  - Update `LlmServiceCollectionExtensions`

- **Configuration**:
  - Unify `Agents:Llm:*` → `Agents:SemanticKernel:*`
  - Maintain backward compatibility for one release cycle

- **Specs**:
  - Add requirements for SK-backed provider
  - Deprecate custom adapter requirements

## Impact

| Area | Change |
|------|--------|
| **Affected specs** | `agents-llm-provider-abstraction` (add SK requirements), `semantic-kernel-integration` (new) |
| **Affected code** | `Gmsd.Agents/Execution/ControlPlane/Llm/**, configuration |
| **Breaking changes** | None for consumers (ILlmProvider contract preserved) |
| **Migration path** | Configuration keys change (with fallback); workflows unchanged |

## Cross-References

- Remediation.md: "Pick a single 'blessed' LLM integration path" (lines 111-135)
- Archived proposal: `changes/archive/2026-02-06-adopt-semantic-kernel/`
- Related spec: `specs/agents-llm-provider-abstraction/`
