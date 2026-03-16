# Design: Standardize on Semantic Kernel for LLM Integration

## Context

The nirmata codebase has a dual-path LLM integration problem:

1. **Semantic Kernel infrastructure exists** (packages v1.70.0, DI registration, provider connectors, tool adapters)
2. **Custom `ILlmProvider` abstraction is actively used** by workflows like `LlmChatResponder`, `PhasePlanner`, `NewProjectInterviewer`

This creates:
- **Maintenance burden**: Two sets of provider connectors to maintain
- **Developer confusion**: Unclear which abstraction to use for new code
- **Incomplete migration**: SK setup exists but workflows don't leverage it

## Goals / Non-Goals

**Goals:**
- Establish a single, production-grade LLM integration path
- Preserve existing workflow contracts (minimize churn)
- Leverage SK's provider connectors, retry logic, and observability
- Maintain evidence capture patterns

**Non-Goals:**
- Rewriting all workflows to use `IChatCompletionService` directly (too much churn)
- Adding new LLM features (streaming, multi-modal) in this change
- Removing `ILlmProvider` entirely (will be deprecated gradually)
- Changing tool system architecture (tools remain `ITool` adapted to SK `KernelFunction`)

## Decisions

### Decision: Adapter pattern over replacement

**What:** Create `SemanticKernelLlmProvider : ILlmProvider` that delegates to `IChatCompletionService`

**Why:**
- Preserves all existing workflow code (no changes to `LlmChatResponder`, `PhasePlanner`, etc.)
- Allows gradual migration: workflows can be updated individually to use `IChatCompletionService` directly
- Reduces risk: single point of integration, well-tested SK path

**Alternatives considered:**
- Replace all `ILlmProvider` usage with `IChatCompletionService`: High churn, touches many files, higher risk
- Keep both paths active: Perpetuates the dual-path problem, unclear guidance

### Decision: SK `IChatCompletionService` as the underlying implementation

**What:** The adapter delegates all LLM operations to SK's `IChatCompletionService`

**Why:**
- SK handles provider-specific quirks (OpenAI tool calling format, Anthropic message structure, etc.)
- Built-in retry policies, logging, telemetry
- Consistent interface across all providers

**Translation layer:**
- `LlmCompletionRequest` → `ChatHistory` + `PromptExecutionSettings`
- `ChatMessageContent` → `LlmCompletionResponse`
- `LlmDelta` ← `StreamingChatMessageContent`

### Decision: Retire custom provider adapters

**What:** Remove `OpenAiLlmAdapter`, `AnthropicLlmAdapter`, `AzureOpenAiLlmAdapter`, `OllamaLlmAdapter`

**Why:**
- SK connectors provide equivalent functionality
- Less code to maintain
- Benefit from SK community updates (new models, API changes)

**Migration:**
- Delete adapter classes
- Remove `AddOpenAiLlm()`, `AddAnthropicLlm()`, etc. extension methods
- Update `AddLlmProvider()` to use `AddSemanticKernel()` + register adapter

### Decision: Deprecate but preserve ILlmProvider contract

**What:** Mark `ILlmProvider` and related types as `[Obsolete]` with message pointing to `IChatCompletionService`

**Why:**
- Signals the direction for new code
- Preserves existing code (no breaking changes)
- Allows gradual migration over multiple changes

**Deprecation message:**
```csharp
[Obsolete("Use Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService directly. " +
          "This abstraction will be removed in a future release.")]
```

### Decision: Configuration unification

**Current state:**
```json
{
  "Agents": {
    "Llm": {
      "Provider": "openai",
      "OpenAi": { "ApiKey": "...", "ModelId": "..." }
    }
  }
}
```

**New state:**
```json
{
  "Agents": {
    "SemanticKernel": {
      "Provider": "openai",
      "OpenAi": { "ApiKey": "...", "ModelId": "..." }
    }
  }
}
```

**Backward compatibility:**
- `AddLlmProvider()` checks `Agents:Llm:Provider` first, falls back to `Agents:SemanticKernel:Provider`
- Warning logged when legacy configuration detected
- Legacy support removed in future release

### Decision: Evidence capture via SK filters

**What:** Keep existing `AosEvidenceFunctionFilter` that implements `IFunctionInvocationFilter`

**Why:**
- Already implemented and working
- Integrates with SK's invocation pipeline
- Writes `LlmCallEnvelope` to `.aos/evidence/`

**No changes needed** to evidence capture for this change.

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           Workflows                                      │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐          │
│  │ LlmChatResponder│  │  PhasePlanner   │  │   Other...      │          │
│  │   (existing)    │  │   (existing)    │  │   (existing)    │          │
│  └────────┬────────┘  └────────┬────────┘  └────────┬────────┘          │
│           │                    │                    │                    │
│           │  injects           │  injects          │  injects           │
│           ▼                    ▼                    ▼                    │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │              ILlmProvider (deprecated, preserved)                │   │
│  │         [Obsolete("Use IChatCompletionService")]                 │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│           │                                                           │
│           │  implements                                               │
│           ▼                                                           │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │           SemanticKernelLlmProvider (NEW)                        │   │
│  │  - Translates LlmCompletionRequest → ChatHistory                 │   │
│  │  - Delegates to IChatCompletionService                          │   │
│  │  - Translates ChatMessageContent → LlmCompletionResponse         │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│           │                                                           │
│           │  injects                                                  │
│           ▼                                                           │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │       IChatCompletionService (from SK Kernel)                    │   │
│  │  - OpenAI connector / Azure OpenAI connector                     │   │
│  │  - Ollama connector / Anthropic connector (custom)               │   │
│  │  - Retry, telemetry, streaming                                   │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│           │                                                           │
│           │  uses                                                     │
│           ▼                                                           │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │              Kernel (SK)                                         │   │
│  │  - Plugins with tools (via KernelPluginFactory)                   │   │
│  │  - Filters (AosEvidenceFunctionFilter)                            │   │
│  └─────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘
```

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| Breaking configuration change | Backward compatibility layer for one release cycle |
| Adapter translation bugs | Comprehensive tests for translation layer; SK connectors are well-tested |
| Performance overhead of adapter | Minimal (just object mapping); can be removed per-workflow later |
| Anthropic connector not official SK | Custom `AnthropicChatCompletionService` already implemented |
| Developer confusion during transition | Clear `[Obsolete]` messages; documentation update |

## Migration Path

### Phase 1: This Change
1. Create `SemanticKernelLlmProvider` adapter
2. Remove custom provider adapters
3. Update `AddLlmProvider()` to use SK
4. Mark `ILlmProvider` as obsolete
5. Add configuration backward compatibility

### Phase 2: Future (separate change)
1. Migrate workflows individually to use `IChatCompletionService` directly
2. Remove `ILlmProvider` adapter when all workflows migrated
3. Remove deprecated types entirely

## Open Questions

- Should we provide a convenience extension `AddLlmProvider(Action<SemanticKernelOptions>)` for programmatic configuration?
- Do we want to expose SK's `Kernel` directly to workflows, or keep it internal?
- Should streaming responses use SK's native streaming or our `LlmDelta` abstraction?
