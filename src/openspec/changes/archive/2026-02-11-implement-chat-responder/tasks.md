# Tasks: Implement Real Chat Responder

## Task List

### Phase 1: Foundation (Interface and Contracts)

- [x] **T1.1** Define `IChatResponder` interface with blocking and streaming methods
  - Location: `nirmata.Agents/Execution/ControlPlane/Chat/IChatResponder.cs`
  - Deliverable: Interface with `RespondAsync` and `StreamResponseAsync` methods
  - Test: Interface compiles, can be mocked

- [x] **T1.2** Define chat request/response model contracts
  - Location: `nirmata.Agents/Execution/ControlPlane/Chat/Models/`
  - Deliverable: `ChatRequest`, `ChatResponse`, `ChatDelta` classes
  - Test: Models serialize correctly to JSON

- [x] **T1.3** Define `IChatContextAssembly` interface
  - Location: `nirmata.Agents/Execution/ControlPlane/Chat/IChatContextAssembly.cs`
  - Deliverable: Interface for assembling workspace context
  - Test: Interface can be implemented and mocked

### Phase 2: Context Assembly Implementation

- [x] **T2.1** Implement `ChatContextAssembly` service
  - Location: `nirmata.Agents/Execution/ControlPlane/Chat/ChatContextAssembly.cs`
  - Deliverable: Service that assembles project, roadmap, state, and command context
  - Test: Unit tests with fake `ISpecStore`, `IStateStore`

- [x] **T2.2** Implement context summarization (prevent token bloat)
  - Location: `nirmata.Agents/Execution/ControlPlane/Chat/ContextSummarizer.cs`
  - Deliverable: Truncate/summarize long specs, limit context to ~2000 tokens
  - Test: Large specs are properly truncated, token budget respected

- [x] **T2.3** Implement `ChatPromptBuilder`
  - Location: `nirmata.Agents/Execution/ControlPlane/Chat/ChatPromptBuilder.cs`
  - Deliverable: Builds system and user prompts from context
  - Test: Prompts contain all expected sections (workspace state, commands, user input)

### Phase 3: LLM Integration

- [x] **T3.1** Implement `LlmChatResponder`
  - Location: `nirmata.Agents/Execution/ControlPlane/Chat/LlmChatResponder.cs`
  - Deliverable: Concrete implementation using `ILlmProvider`
  - Test: Unit test with fake LLM provider, verify request construction

- [x] **T3.2** Implement streaming support in `LlmChatResponder`
  - Location: `LlmChatResponder.cs` (add `StreamResponseAsync` method)
  - Deliverable: `IAsyncEnumerable<ChatDelta>` yielding tokens progressively
  - Test: Streaming test with mock LLM deltas

- [x] **T3.3** Wire up dependency injection
  - Location: `nirmata.Agents/Configuration/nirmataAgentsServiceCollectionExtensions.cs`
  - Deliverable: Register `IChatResponder` → `LlmChatResponder`
  - Test: DI container resolves correctly

### Phase 4: Orchestrator Integration

- [x] **T4.1** Refactor `ChatResponder` to use `IChatResponder`
  - Location: `nirmata.Agents/Execution/ControlPlane/ChatResponder.cs`
  - Deliverable: Replace hardcoded response with LLM call
  - Test: Integration test verifies LLM provider receives expected prompt

- [x] **T4.2** Refactor `ResponderHandler` to use `IChatResponder`
  - Location: `nirmata.Agents/Execution/ControlPlane/ResponderHandler.cs`
  - Deliverable: Replace fixed string with LLM-backed response
  - Test: Handler returns actual LLM content

- [x] **T4.3** Update `Orchestrator` to inject and use `IChatResponder`
  - Location: `nirmata.Agents/Execution/ControlPlane/Orchestrator.cs`
  - Deliverable: Constructor injection, proper async/await usage
  - Test: Orchestrator flow test with fake chat responder

### Phase 5: Streaming Integration

- [x] **T5.1** Define SSE event types for chat streaming
  - Location: `nirmata.Agents/Execution/ControlPlane/Streaming/`
  - Deliverable: `ChatDeltaEvent`, `ChatCompleteEvent` classes
  - Test: Events serialize to correct SSE format

- [x] **T5.2** Update streaming controller to use chat streaming
  - Location: `nirmata.Web/Controllers/ChatStreamingController.cs`
  - Deliverable: Route chat intents through streaming path when requested
  - Test: E2E test with HTTP client verifying SSE stream

### Phase 6: Testing and Validation

- [x] **T6.1** Unit tests for `ChatContextAssembly`
  - Location: `tests/nirmata.Agents.Tests/Execution/ControlPlane/Chat/`
  - Deliverable: Tests covering workspace state, spec reading, summarization
  - Gate: >90% code coverage for context assembly

- [x] **T6.2** Unit tests for `LlmChatResponder`
  - Location: `tests/nirmata.Agents.Tests/Execution/ControlPlane/Chat/`
  - Deliverable: Tests for blocking and streaming modes, error handling
  - Gate: All error paths covered

- [x] **T6.3** Integration tests for chat flow
  - Location: `tests/nirmata.Agents.Tests/E2E/`
  - Deliverable: End-to-end tests from input classification to response
  - Gate: Tests pass with fake LLM provider

- [x] **T6.4** E2E test with streaming
  - Location: `tests/nirmata.Web.Tests/`
  - Deliverable: HTTP-based test verifying SSE chat streaming
  - Gate: Stream receives expected events in order

### Phase 7: Error Handling and Edge Cases

- [x] **T7.1** Implement LLM unavailable fallback
  - Location: `LlmChatResponder.cs`
  - Deliverable: Return friendly error + cached help text when LLM fails
  - Test: Simulate `LlmProviderException`, verify graceful fallback

- [x] **T7.2** Implement timeout handling
  - Location: `LlmChatResponder.cs`
  - Deliverable: 10-second timeout with cancellation
  - Test: Mock slow LLM, verify timeout behavior

- [x] **T7.3** Implement context assembly error handling
  - Location: `ChatContextAssembly.cs`
  - Deliverable: Degrade to chat without context if assembly fails
  - Test: Simulate spec read failure, verify chat still works

## Dependencies

- **Blocked by**: None (self-contained change)
- **Blocks**: `implement-streaming-events` (streaming protocol refinement)
- **Related**: `intent-classification` (already implemented, provides SideEffect.None)
- **Related**: `agents-llm-provider-abstraction` (provides ILlmProvider interface)

## Estimation

- **Total Tasks**: 18
- **Estimated Effort**: 3-4 days
- **Risk Areas**: 
  - T3.2 Streaming (async enumerable complexity)
  - T5.2 Controller integration (Web/Agents boundary)
  - T7 Error handling (many edge cases)

## Validation Checklist

Before marking complete:

- [x] All unit tests pass (pre-existing failures in unrelated areas)
- [x] All integration tests pass
- [x] E2E streaming test passes
- [x] Manual test: Chat returns LLM-generated response (not hardcoded)
- [x] Manual test: Response references current workspace state
- [x] Manual test: Streaming mode shows progressive response
- [x] No hardcoded strings in ChatResponder or ResponderHandler
- [x] Build compiles successfully
