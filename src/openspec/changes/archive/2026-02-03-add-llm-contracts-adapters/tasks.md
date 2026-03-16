## 1. LLM Contracts (vendor-neutral abstraction)
- [x] 1.1 Define `ILlmProvider` interface with `CompleteAsync` method
- [x] 1.2 Define `LlmMessage` record (Role, Content, ToolCalls, etc.)
- [x] 1.3 Define `LlmCompletionRequest` and `LlmCompletionResponse` types
- [x] 1.4 Define `LlmToolCall` and `LlmToolResult` for normalized tool-call representation
- [x] 1.5 Define `LlmProviderOptions` for temperature, max tokens, model selection
- [x] 1.6 Add XML documentation and nullability annotations

## 2. Provider Adapter Scaffolds
- [x] 2.1 Create `OpenAiLlmAdapter` implementing `ILlmProvider`
- [x] 2.2 Create `AnthropicLlmAdapter` implementing `ILlmProvider`
- [x] 2.3 Create `AzureOpenAiLlmAdapter` implementing `ILlmProvider`
- [x] 2.4 Create `OllamaLlmAdapter` implementing `ILlmProvider`
- [x] 2.5 Implement message format translation (native ↔ normalized)
- [x] 2.6 Implement tool-call format translation

## 3. Dependency Injection and Configuration
- [x] 3.1 Create `LlmServiceCollectionExtensions` with `AddLlmProvider()` method
- [x] 3.2 Create `AddOpenAiLlm()`, `AddAnthropicLlm()`, etc. extension methods
- [x] 3.3 Bind configuration section `Aos:Llm:*` to provider-specific options
- [x] 3.4 Register singleton `ILlmProvider` based on `Aos:Llm:Provider` config value
- [x] 3.5 Add validation: throw `InvalidOperationException` if provider unconfigured

## 4. Prompt Template System
- [x] 4.1 Define `IPromptTemplateLoader` interface with `GetById(string id)` method
- [x] 4.2 Define `PromptTemplate` record (Id, Content, Metadata)
- [x] 4.3 Create `EmbeddedResourcePromptLoader` implementation
- [x] 4.4 Create `nirmata.Aos/Resources/Prompts/` directory structure
- [x] 4.5 Add sample prompt templates (e.g., `planning.task-breakdown.v1.prompt.txt`)
- [x] 4.6 Register loader in DI container

## 5. Evidence Capture
- [x] 5.1 Define `LlmCallEnvelope` record extending call envelope contract
- [x] 5.2 Integrate `IAosEvidenceWriter` in adapter base or decorator
- [x] 5.3 Capture request/response metadata (model, tokens, timing)
- [x] 5.4 Write envelopes to `.aos/evidence/runs/<run-id>/logs/llm-*.json`

## 6. Testing
- [x] 6.1 Create `FakeLlmProvider` for unit testing (returns configurable responses)
- [x] 6.2 Write tests: DI resolves correct provider by configuration
- [x] 6.3 Write tests: template loader returns expected content by ID
- [x] 6.4 Write tests: message normalization round-trips correctly
- [x] 6.5 Write tests: tool-call serialization produces valid JSON schema
