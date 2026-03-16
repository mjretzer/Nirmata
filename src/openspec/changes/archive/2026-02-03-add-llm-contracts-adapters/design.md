# Design: add-llm-contracts-adapters

## Context
The AOS engine requires LLM capabilities for planning, task execution, and verification workflows. Rather than coupling directly to a single provider (e.g., OpenAI), we need a vendor-neutral abstraction that supports:
- Multiple providers (OpenAI, Anthropic, Azure OpenAI, Ollama)
- Deterministic configuration-driven provider selection
- Prompt templates stored as external assets
- Consistent evidence capture for all LLM calls

This spans contracts, adapters, configuration, and resource loading.

## Goals / Non-Goals

**Goals:**
- Define stable, versioned LLM contracts (`ILlmProvider`, message types, streaming options)
- Provide request/response normalization across providers
- Support tool-call representation normalization between LLM formats and AOS `ITool`
- Enable configuration-driven provider selection via DI
- Load prompt templates by ID from embedded resources or files
- Record LLM calls as auditable evidence envelopes

**Non-Goals:**
- Full implementation of all provider adapters (scaffolds only)
- Fine-tuned model management or training pipelines
- Advanced prompt chaining/orchestration (higher-level workflows will use these primitives)
- Non-LLM AI providers (image generation, etc.)

## Decisions

**Decision: Use adapter pattern with normalized message types**
- Rationale: Each provider has different message/tool-call formats; normalization keeps orchestration code clean
- Alternatives considered: 
  - Raw provider SDK passthrough (rejected: couples engine to vendor SDKs)
  - Generic JSON payloads (rejected: loses type safety and IntelliSense)

**Decision: Embed prompt templates as resources with runtime loader abstraction**
- Rationale: Enables versioning with code while allowing future override mechanisms
- Location: `nirmata.Aos/Resources/Prompts/*.prompt.txt` or `*.md`
- Loader interface: `IPromptTemplateLoader.GetById(string id)`

**Decision: DI registration selects provider via configuration**
- Rationale: Single deployment can target different providers without rebuild
- Config key: `Aos:Llm:Provider` → maps to `AddOpenAiLlm()`, `AddAnthropicLlm()`, etc.
- Default: throws if unconfigured (fail-fast for missing critical dependency)

**Decision: Tool-call normalization uses existing `ITool`/`ToolRequest` contracts**
- Rationale: Reuse established tool abstraction; LLM adapters translate between native tool format and `ITool`
- Tool definitions passed to LLM use JSON schema derived from `ToolDescriptor`

## Risks / Trade-offs

- **Risk:** Provider API drift breaks adapter mapping  
  → Mitigation: Adapter unit tests with recorded responses; versioned adapter namespaces
- **Risk:** Prompt template proliferation  
  → Mitigation: Namespace conventions `domain.purpose.version` (e.g., `planning.task-breakdown.v1`)
- **Risk:** Streaming response complexity  
  → Mitigation: Initial implementation returns complete responses; streaming interface added as enhancement

## Migration Plan
Not applicable — new capability, no existing migrations.

## Open Questions
- [ ] Should we support model selection per-call (via request) or per-deployment (via config)?
- [ ] Should prompt templates support variable substitution syntax (e.g., Mustache, T4, or custom)?
