# finalize-llm-provider Design

## Architecture Overview

This change finalizes the LLM provider layer by standardizing on Semantic Kernel as the production-ready abstraction. The design leverages the existing `SemanticKernelLlmProvider` implementation while ensuring robust structured outputs and establishing a clear expansion path.

## Current State Analysis

### Existing Implementation
- `SemanticKernelLlmProvider` delegates to SK's `IChatCompletionService`
- `ILlmProvider` interface is marked obsolete with SK migration guidance
- Structured output validation exists via `LlmStructuredOutputSchema`
- Tool calling flows through SK's `Kernel` and `KernelFunction` system
- Configuration unified under `Agents:SemanticKernel` section

### Identified Gaps
- Production hardening (error handling, logging, retry logic)
- Comprehensive testing of structured outputs for planners
- End-to-end validation of chat and tool calling workflows
- Clear provider expansion documentation and patterns

## Design Decisions

### 1. Semantic Kernel as Standard Abstraction
**Decision**: Standardize on Semantic Kernel's `IChatCompletionService` as the primary LLM abstraction.

**Rationale**:
- SK provides vendor-neutral interfaces with provider-specific connectors
- Eliminates need for custom adapter implementations
- Microsoft-backed with active development and support
- Built-in support for tool calling, streaming, and structured outputs

**Trade-offs**:
- Adds Semantic Kernel dependency (already present)
- Less control over provider-specific optimizations
- Potential SK version compatibility considerations

### 2. Structured Output Validation Strategy
**Decision**: Use JSON Schema validation with strict enforcement for planner workflows.

**Rationale**:
- Ensures planners produce reliable, parseable artifacts
- Leverages existing `LlmStructuredOutputSchema` infrastructure
- Provides clear error messages for validation failures
- Works across all providers via SK's response format support

**Implementation**:
- Schema validation in `SemanticKernelLlmProvider.CompleteAsync()`
- Strict validation mode for critical planner outputs
- Graceful degradation for non-critical workflows

### 3. Provider Expansion Pattern
**Decision**: Use Semantic Kernel connectors for provider expansion.

**Rationale**:
- Consistent pattern across all providers
- Leverages SK's connector ecosystem
- Minimal code changes for new providers
- Centralized configuration management

**Expansion Path**:
1. OpenAI (current baseline)
2. Anthropic (via custom SK connector or community connector)
3. Azure OpenAI (via SK Azure connector)
4. Local models (via Ollama SK connector)

## Implementation Strategy

### Phase 1: Production Hardening
- Enhance error handling and logging in `SemanticKernelLlmProvider`
- Add retry logic for transient failures
- Improve configuration validation and error messages
- Add comprehensive test coverage

### Phase 2: Structured Output Validation
- Verify schema validation works for all planner types
- Add integration tests for validation edge cases
- Optimize validation performance
- Document schema usage patterns

### Phase 3: End-to-End Validation
- Test chat responder with various conversation patterns
- Verify tool calling works through SK's function invocation
- Validate evidence capture for all interactions
- Test streaming scenarios and cancellation

### Phase 4: Provider Expansion Foundation
- Document expansion patterns using SK connectors
- Research and document Anthropic integration options
- Update configuration schema for multi-provider support
- Create provider selection framework

## Technical Considerations

### Error Handling
- Distinguish between retryable and non-retryable errors
- Provide clear error messages for configuration issues
- Log sufficient context for debugging production issues
- Handle provider-specific error formats gracefully

### Performance
- Minimize schema validation overhead
- Optimize serialization/deserialization for tool calls
- Consider connection pooling and request batching
- Monitor token usage and costs

### Configuration
- Support both new and legacy configuration schemas
- Validate configuration at startup with clear errors
- Provide migration path for legacy configurations
- Document all configuration options with examples

### Testing Strategy
- Unit tests for individual components
- Integration tests for provider interactions
- End-to-end tests for complete workflows
- Performance tests for validation overhead

## Risk Assessment

### High Risk
- **Structured output validation breaking existing workflows**
  - *Mitigation:* Add feature flag for strict validation; default to lenient mode; test with real planner outputs before enabling
  - *Acceptance:* Validation must pass 100% of existing planner test cases
  
- **Performance regression from schema validation**
  - *Mitigation:* Implement schema caching; measure baseline before changes; target < 50ms per validation
  - *Acceptance:* Validation overhead must not exceed 10% of total request time
  
- **Configuration migration issues**
  - *Mitigation:* Support both old and new config formats; provide migration guide; test in staging first
  - *Acceptance:* Existing deployments must start without code changes

### Medium Risk
- **Semantic Kernel version compatibility**
  - *Mitigation:* Pin SK version in Directory.Build.props; test with current stable version; document upgrade path
  - *Acceptance:* Must support SK 1.x stable releases
  
- **Provider-specific edge cases not handled by SK**
  - *Mitigation:* Add comprehensive error handling; log provider-specific errors; create issue tracker for gaps
  - *Acceptance:* All SK exceptions must be caught and converted to `LlmProviderException`
  
- **Evidence capture gaps in new workflows**
  - *Mitigation:* Add `AosEvidenceFunctionFilter` integration tests; verify evidence for all code paths
  - *Acceptance:* 100% of LLM calls and tool invocations must have evidence

### Low Risk
- **Documentation completeness**
  - *Mitigation:* Create templates and examples; peer review before release
  - *Acceptance:* All public APIs must have XML documentation
  
- **Test coverage gaps**
  - *Mitigation:* Use code coverage tools; aim for >90% coverage in LLM provider code
  - *Acceptance:* Critical paths (error handling, validation) must have >95% coverage
  
- **Future provider expansion complexity**
  - *Mitigation:* Design with extensibility in mind; document patterns; create base classes for reuse
  - *Acceptance:* Adding Anthropic should take <2 days with documented pattern

## Success Metrics

### Functional Metrics
- 100% of planner workflows produce valid structured outputs
- Chat responder handles natural conversation without errors
- Tool calling works reliably across all supported providers
- Configuration errors are caught at startup with clear messages

### Quality Metrics
- Test coverage > 90% for LLM provider components
- Zero production errors related to LLM provider implementation
- Schema validation performance impact < 50ms per request
- Documentation completeness score > 95%

### Operational Metrics
- Mean time to resolve LLM-related issues < 1 hour
- Configuration setup time < 15 minutes for new deployments
- Provider expansion time < 2 days for new connectors
