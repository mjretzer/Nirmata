# finalize-llm-provider Tasks

## Implementation Tasks

### 1. Production-harden SemanticKernelLlmProvider
- [x] 1.1 Review `nirmata.Agents/Execution/ControlPlane/Llm/SemanticKernelLlmProvider.cs` for error handling gaps
  - [x] Identify all exception types that can be thrown by SK's IChatCompletionService
  - [x] Classify as retryable (network, timeout, rate limit) vs non-retryable (auth, invalid model)
  - [x] Add custom `LlmProviderException` with `isRetryable` flag
- [x] 1.2 Implement structured logging in `SemanticKernelLlmProvider`
  - [x] Log request start with provider, model, correlation ID
  - [x] Log token usage when available from SK response
  - [x] Log errors without exposing API keys or sensitive prompts
  - [x] Use `ILogger<SemanticKernelLlmProvider>` with appropriate log levels
- [x] 1.3 Add exponential backoff retry logic
  - [x] Implement `IRetryPolicy` or use Polly for transient failures
  - [x] Max 3 retries with 100ms base delay + jitter
  - [x] Log each retry attempt with attempt number
- [x] 1.4 Add configuration validation at DI registration time
  - [x] Validate `Agents:SemanticKernel:ApiKey` is present and non-empty
  - [x] Validate `Agents:SemanticKernel:Model` is in supported list (gpt-4, gpt-4-turbo, etc.)
  - [x] Throw `InvalidOperationException` with clear guidance on missing/invalid config
- [x] 1.5 Add unit tests for error handling in `tests/nirmata.Agents.Tests/Execution/ControlPlane/Llm/SemanticKernelLlmProviderTests.cs`
  - [x] Test network timeout → retryable exception
  - [x] Test auth error → non-retryable exception
  - [x] Test invalid config → startup validation failure

### 2. Verify Planner Structured Outputs
- [x] 2.1 Test all planner types with structured output schemas
  - [x] Identify all planner implementations (FixPlanner, TaskExecutor, etc.)
  - [x] Verify each produces valid JSON matching its schema
  - [x] Test with real LLM responses from OpenAI
- [x] 2.2 Add schema caching for performance
  - [x] Implement schema compilation cache in `SemanticKernelLlmProvider`
  - [x] Verify cache hit rate > 90% for repeated schemas
  - [x] Measure validation time: target < 50ms per request
- [x] 2.3 Add integration tests in `tests/nirmata.Agents.Tests/Execution/ControlPlane/Llm/StructuredOutputValidationTests.cs`
  - [x] Test valid planner output passes validation
  - [x] Test invalid JSON fails with clear error message
  - [x] Test schema with `additionalProperties: false` rejects extra fields
  - [x] Test empty response is caught and reported
- [x] 2.4 Document schema usage patterns in `nirmata.Agents/README.md`
  - [x] Show how to define a structured output schema
  - [x] Show how to use schema in planner workflows
  - [x] Provide troubleshooting guide for validation failures

### 3. End-to-End Chat Responder Validation  
- [x] 3.1 Test chat responder with multi-turn conversations
  - [x] Create test scenarios with 3+ message exchanges
  - [x] Verify context is maintained across turns
  - [x] Verify responses are contextually appropriate
- [x] 3.2 Test streaming without message loss
  - [x] Send long-form LLM request (>1000 tokens)
  - [x] Verify all chunks arrive in order
  - [x] Verify assembled content matches non-streamed response
  - [x] Test with cancellation mid-stream
- [x] 3.3 Add end-to-end tests in `tests/nirmata.Agents.Tests/Execution/ChatResponderE2ETests.cs`
  - [x] Test multi-turn conversation flow
  - [x] Test streaming response completeness
  - [x] Test cancellation handling
  - [x] Test error recovery

### 4. Tool Calling Integration Testing
- [x] 4.1 Verify tool calling through SK's function invocation
  - [x] Test single tool call execution
  - [x] Test multiple parallel tool calls
  - [x] Test tool result propagation back to LLM
  - [x] Verify SK's `KernelFunction` integration works correctly
- [x] 4.2 Test complex tool scenarios
  - [x] Test tool that calls another tool (nested)
  - [x] Test tool that fails and error is reported to LLM
  - [x] Test tool with large result (>10KB)
- [x] 4.3 Add tool calling tests in `tests/nirmata.Agents.Tests/Execution/ControlPlane/Llm/ToolCallingTests.cs`
  - [x] Test single tool invocation
  - [x] Test parallel tool calls
  - [x] Test tool failure handling
  - [x] Test evidence capture for each tool call
- [x] 4.4 Verify evidence capture for tool interactions
  - [x] Confirm `AosEvidenceFunctionFilter` captures SK function calls
  - [x] Verify evidence includes function name, plugin, arguments
  - [x] Verify evidence is linked to parent LLM call

### 5. Configuration and DI Validation
- [x] 5.1 Review configuration schema in `nirmata.Agents/Configuration/SemanticKernelOptions.cs`
  - [x] Ensure all required fields are validated
  - [x] Add XML documentation for each option
  - [x] Add validation attributes (Required, Range, etc.)
- [x] 5.2 Test DI registration in `nirmata.Agents/Composition/ServiceCollectionExtensions.cs`
  - [x] Test registration with valid configuration
  - [x] Test registration with missing API key (should throw)
  - [x] Test registration with invalid model (should throw)
  - [x] Test in different host environments (Windows Service, Web API)
- [x] 5.3 Add configuration tests in `tests/nirmata.Agents.Tests/Configuration/SemanticKernelOptionsTests.cs`
  - [x] Test valid configuration loads correctly
  - [x] Test missing required fields are caught
  - [x] Test invalid values are rejected
- [x] 5.4 Document configuration in `nirmata.Agents/README.md`
  - [x] Show example `appsettings.json` with all options
  - [x] Document each configuration key and its purpose
  - [x] Provide troubleshooting for common configuration errors

### 6. Foundation for Provider Expansion
- [x] 6.1 Research Semantic Kernel Anthropic integration options
  - [x] Check if SK has official Anthropic connector
  - [x] Evaluate community connectors (if any)
  - [x] Document findings in `openspec/changes/finalize-llm-provider/anthropic-research.md`
- [x] 6.2 Design provider selection pattern
  - [x] Create factory interface for provider instantiation
  - [x] Design configuration schema for multiple providers
  - [x] Document how to add new provider without modifying selection logic
- [x] 6.3 Update configuration schema to support multiple providers
  - [x] Add `Agents:SemanticKernel:Provider` setting (default: "openai")
  - [x] Add provider-specific sections: `Agents:SemanticKernel:OpenAI:*`, `Agents:SemanticKernel:Anthropic:*`
  - [x] Add validation to ensure selected provider has required config
- [x] 6.4 Create provider expansion documentation
  - [x] Document in `nirmata.Agents/PROVIDER_EXPANSION.md`
  - [x] Step-by-step guide for adding new provider
  - [x] Include Anthropic as worked example
  - [x] Document testing requirements for new providers
- [x] 6.5 Create Anthropic configuration schema (foundation only, not implementation)
  - [x] Define `Agents:SemanticKernel:Anthropic:ApiKey`
  - [x] Define `Agents:SemanticKernel:Anthropic:Model` (claude-3-opus, etc.)
  - [x] Define optional settings (temperature, max-tokens, etc.)

## Validation Tasks

### 7. Comprehensive Testing
- [x] Add unit tests for enhanced `SemanticKernelLlmProvider` features
  - [x] Existing tests in `SemanticKernelLlmProviderTests.cs` cover error handling, logging, retry logic, and configuration
  - [x] Tests verify network timeout → retryable exception, auth error → non-retryable exception, invalid config → startup validation failure
- [x] Add integration tests for structured output validation
  - [x] Existing tests in `StructuredOutputValidationTests.cs` cover valid/invalid JSON, missing required fields, additionalProperties validation
  - [x] Tests verify schema caching performance and complex nested schema validation
- [x] Add end-to-end tests for chat and tool calling scenarios
  - [x] Existing tests in `ChatResponderE2ETests.cs` cover multi-turn conversations, streaming, cancellation, error recovery
  - [x] Existing tests in `ToolCallingTests.cs` cover single/parallel tool calls, failure handling, evidence capture
- [x] Add performance tests for schema validation overhead
  - [x] New `SchemaValidationPerformanceTests.cs` validates schema validation completes in < 50ms
  - [x] Tests verify cache hit rate > 90% for repeated schemas
  - [x] Tests verify validation overhead < 10% of total request time

### 8. Documentation and Examples
- [x] Update configuration documentation with examples
  - [x] README.md includes OpenAI, Azure OpenAI, Ollama, and Anthropic configuration examples
  - [x] Configuration validation section documents error messages and troubleshooting
- [x] Document structured output schema usage patterns
  - [x] README.md includes schema definition, usage in planner workflows, validation features, and performance considerations
  - [x] Troubleshooting section documents common validation errors and solutions
- [x] Create troubleshooting guide for common LLM issues
  - [x] New `LLM_TROUBLESHOOTING.md` documents configuration, runtime, structured output, tool calling, and performance issues
  - [x] Includes diagnosis steps, solutions, and code examples for each category
- [x] Document provider expansion process for future developers
  - [x] `PROVIDER_EXPANSION.md` includes step-by-step guide for adding new providers
  - [x] Documents provider selection pattern, validation, registration, and testing requirements
  - [x] Includes worked example for Anthropic provider

## Verification Criteria
- All planner workflows produce valid structured outputs
- Chat responder handles natural conversation with streaming
- Tool calling works reliably with proper evidence capture
- Configuration is robust with clear error messages
- Test coverage meets quality standards
- Documentation is complete and accurate
