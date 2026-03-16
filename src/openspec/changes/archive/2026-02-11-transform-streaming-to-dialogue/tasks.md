# transform-streaming-to-dialogue — Tasks

## Phase 1: Protocol Foundation

### T1: Define Streaming Event Schema
**Status**: completed  
**Assignee**: Cascade  
**Depends**: None  

Create the JSON schema and C# models for streaming dialogue events:
- [x] Define `StreamingEvent` base envelope class in `nirmata.Web/Models/Streaming`
- [x] Create event payload classes: `IntentClassifiedPayload`, `GateSelectedPayload`, `ToolCallPayload`, `ToolResultPayload`, `PhaseLifecyclePayload`, `AssistantDeltaPayload`, `AssistantFinalPayload`
- [x] Create JSON schema file `StreamingEventSchema.json` for validation
- [x] Add enum for event types (`StreamingEventType`)
- [x] Unit tests for serialization/deserialization

**Validation**: All event types round-trip through JSON serialization correctly.

---

### T2: Create IEventSink Interface
**Status**: completed  
**Assignee**: Cascade  
**Depends**: T1  

Define the event emission abstraction:
- [x] Create `IEventSink` interface with `Emit()` methods
- [x] Implement `ChannelEventSink` using `Channel<StreamingEvent>`
- [x] Implement `NullEventSink` no-op implementation
- [x] Add extension methods for common event emission patterns
- [x] Unit tests for event sink implementations

**Validation**: Events emitted to `ChannelEventSink` are readable from channel reader.

---

### T3: Create IStreamingOrchestrator Interface
**Status**: completed  
**Assignee**: Cascade  
**Depends**: T2  

Define the streaming-capable orchestrator contract:
- [x] Create `IStreamingOrchestrator` interface
- [x] Define `ExecuteWithEventsAsync` method returning `IAsyncEnumerable<StreamingEvent>`
- [x] Create `StreamingOrchestrationOptions` configuration class
- [x] Add documentation for usage patterns

**Validation**: Interface compiles and can be mocked for testing.

---

## Phase 2: Orchestrator Integration

### T4: Implement StreamingOrchestrator
**Status**: completed  
**Assignee**: Cascade  
**Depends**: T3  

Create the streaming wrapper around the existing orchestrator:
- [x] Implement `StreamingOrchestrator` class wrapping `IOrchestrator`
- [x] Add event emission hooks at classification decision point
- [x] Add event emission hooks at gating decision point
- [x] Add event emission hooks for run lifecycle
- [x] Ensure proper channel completion on orchestration end
- [x] Integration tests with fake orchestrator

**Validation**: Streaming orchestrator emits events in correct sequence.

---

### T5: Add Intent Classification Event Emission
**Status**: completed  
**Assignee**: Cascade  
**Depends**: T4  

Emit `intent.classified` events:
- [x] Modify classifier to expose confidence scores
- [x] Capture classification reasoning from LLM response
- [x] Emit `intent.classified` with category, confidence, reasoning
- [x] Handle classification errors with `error` event

**Validation**: Classifying "hello" emits Chat with high confidence; "plan phase" emits Write.

---

### T6: Add Gate Selection Event Emission
**Status**: completed  
**Assignee**: Cascade  
**Depends**: T5  

Emit `gate.selected` events:
- [x] Modify gating engine to expose selection reasoning
- [x] Build `ProposedAction` structure for confirmation flows
- [x] Emit `gate.selected` with phase, reasoning, confirmation flag
- [x] Handle gating errors with `error` event

**Validation**: Gating to Planner phase emits event with Planner target and reasoning.

---

### T7: Add Run Lifecycle Event Emission
**Status**: completed  
**Assignee**: Cascade  
**Depends**: T6  

Emit `run.started` and `run.finished` events:
- [x] Emit `run.started` when write workflow begins
- [x] Emit `run.finished` when workflow completes (success/failure)
- [x] Suppress run events for Chat-only interactions
- [x] Include run duration and artifact references

**Validation**: Chat interactions emit NO run events; write workflows emit start and finish.

---

### T8: Add Phase Lifecycle Event Emission
**Status**: completed  
**Assignee**: Cascade  
**Depends**: T7  

Emit `phase.started` and `phase.completed` events:
- [x] Instrument phase handlers to emit start events
- [x] Instrument phase handlers to emit completion events
- [x] Include phase context and output artifacts
- [x] Handle phase execution errors

**Validation**: Executing a phase emits start, then completion with artifacts.

---

### T9: Add Tool Call Event Emission
**Status**: completed  
**Assignee**: Cascade  
**Depends**: T8  

Emit `tool.call` and `tool.result` events:
- [x] Instrument tool invocation to emit call events
- [x] Instrument tool execution to emit result events
- [x] Generate unique `callId` for correlation
- [x] Include execution duration in results

**Validation**: Tool execution emits call with callId, then result with matching callId.

---

### T10: Add Assistant Dialogue Event Emission
**Status**: completed  
**Assignee**: Cascade  
**Depends**: T9  

Emit `assistant.delta` and `assistant.final` events:
- [x] Integrate with `ILlmProvider` streaming interface
- [x] Emit `assistant.delta` for each token chunk
- [x] Emit `assistant.final` when response completes
- [x] Support non-streaming responses (direct final)

**Validation**: Assistant response streams via deltas, then finalizes.

---

## Phase 3: UI Rendering

### T11: Create Event Renderer Registry
**Status**: completed  
**Assignee**: Cascade  
**Depends**: T1  

Build the UI event renderer system:
- [x] Create `IEventRenderer` interface
- [x] Implement `EventRendererRegistry` singleton
- [x] Create default/fallback renderer for unknown events
- [x] Add TypeScript type definitions for events
- [x] Unit tests for renderer resolution

**Validation**: Registry correctly resolves renderers by event type.

---

### T12: Implement Reasoning Block Renderers
**Status**: completed  
**Assignee**: Cascade  
**Depends**: T11  

Create renderers for reasoning events:
- [x] Implement `intent.classified` renderer with confidence visualization
- [x] Implement `gate.selected` renderer with decision card
- [x] Add collapsible behavior for reasoning blocks
- [x] Implement confirmation button handling

**Validation**: Reasoning events render as styled, collapsible blocks.

---

### T13: Implement Operation Event Renderers
**Status**: completed  
**Assignee**: Cascade  
**Depends**: T12  

Create renderers for operation events:
- [x] Implement `run.started/finished` renderers
- [x] Implement `phase.started/completed` renderers with timeline
- [x] Implement `tool.call` renderer with pending state
- [x] Implement `tool.result` renderer updating call cards

**Validation**: Operation events render as status indicators and cards.

---

### T14: Implement Assistant Message Renderers
**Status**: completed  
**Assignee**: Cascade  
**Depends**: T13  

Create renderers for assistant dialogue:
- [x] Implement `assistant.delta` renderer with streaming text
- [x] Implement `assistant.final` renderer with structured data
- [x] Add rich content rendering (tables, cards, code)
- [x] Support event correlation by messageId

**Validation**: Assistant messages stream in real-time and display rich content.

---

### T15: Implement Error Renderer
**Status**: completed  
**Assignee**: Cascade  
**Depends**: T14  

Create error event renderer:
- [x] Implement `error` renderer with alert styling
- [x] Add severity-based visual indicators
- [x] Implement retry button for recoverable errors
- [x] Link errors to phase context

**Validation**: Error events display as alert banners with appropriate actions.

---

## Phase 4: Integration

### T16: Create v2 Streaming Endpoint
**Status**: completed  
**Assignee**: Cascade  
**Depends**: T4, T11  

Add new API endpoint for typed event streaming:
- [x] Create `POST /api/chat/stream-v2` endpoint
- [x] Wire up `StreamingOrchestrator` to controller
- [x] Implement SSE response with typed events
- [x] Add legacy endpoint preservation at `/api/chat/stream`
- [x] Integration tests for full flow

**Validation**: v2 endpoint returns typed events; legacy endpoint still works.

---

### T17: Add HTMX SSE Integration
**Status**: completed  
**Assignee**: Cascade  
**Depends**: T16  

Integrate event rendering with HTMX:
- [x] Configure HTMX SSE extension for v2 endpoint
- [x] Map SSE events to renderer invocations
- [x] Handle event sequencing and ordering
- [x] Add event debouncing for rapid deltas

**Validation**: HTMX correctly receives and renders streaming events.

---

### T18: Implement Event Sequencing
**Status**: completed  
**Assignee**: Cascade  
**Depends**: T17  

Ensure correct event ordering in UI:
- [x] Add client-side event buffer for reordering
- [x] Implement timestamp-based sorting
- [x] Handle out-of-order event arrival
- [x] Add sequence number validation (optional)

**Validation**: Events always display in logical order regardless of arrival order.

---

### T19: Add Backward Compatibility Layer
**Status**: completed  
**Assignee**: Cascade  
**Depends**: T16  

Support legacy clients:
- [x] Create legacy event adapter
- [x] Transform typed events to legacy format
- [x] Support Accept header negotiation
- [x] Document migration path for clients

**Validation**: Legacy clients receive compatible event format.

---

## Phase 5: Validation & Hardening

### T20: Unit Tests for Event System
**Status**: completed  
**Assignee**: Cascade  
**Depends**: T1-T10  

Comprehensive unit test coverage:
- [x] Test all event payload serialization (StreamingEventSerializationTests.cs)
- [x] Test event sink implementations (EventSinkTests.cs)
- [x] Test streaming orchestrator wrapper (StreamingOrchestratorTests.cs)
- [x] Test individual event emission points

**Validation**: >90% code coverage for event system.

---

### T21: Integration Tests for End-to-End Flow
**Status**: completed  
**Assignee**: Cascade  
**Depends**: T19  

Integration test scenarios:
- [x] Test chat-only flow (no run events)
- [x] Test write workflow with confirmation
- [x] Test multi-phase workflow with tool calls
- [x] Test error scenarios and recovery
- [x] Test legacy client compatibility

**Validation**: All integration test scenarios pass.

---

### T22: Performance Benchmarking
**Status**: completed  
**Assignee**: Cascade  
**Depends**: T21  

Validate performance targets:
- [x] Measure event emission latency (< 50ms)
- [x] Measure UI render latency (< 50ms per event)
- [x] Test with 100+ event sequences
- [x] Memory usage profiling

**Implementation**:
- C# performance tests: `tests/nirmata.Web.Tests/Models/Streaming/StreamingPerformanceTests.cs`
  - Event emission latency tests (single, multiple, burst scenarios)
  - JSON serialization latency tests (server-side rendering equivalent)
  - 100+ event sequence stress tests (100, 200, 500 events)
  - Memory profiling tests (event creation, buffered events, serialization)
- JS performance tests: `tests/nirmata.Web/js/streaming-performance.test.js`
  - IntentClassifiedRenderer performance (< 50ms avg, < 75ms p95)
  - GateSelectedRenderer performance (< 50ms avg)
  - AssistantDeltaRenderer performance (< 50ms avg, < 100ms max)
  - EventSequencer performance (100 events < 200ms, bounded memory)

**Validation**: All performance targets met. Event emission < 50ms, serialization < 50ms, UI renderers < 50ms average.

---

### T23: Documentation
**Status**: completed  
**Assignee**: Cascade  
**Depends**: T22  

Create comprehensive documentation:
- [x] API documentation for v2 streaming endpoint
- [x] Event type reference guide
- [x] UI renderer development guide
- [x] Migration guide from legacy streaming
- [x] Architecture decision record (ADR)

**Validation**: Documentation reviewed and complete.

---

## Completion Criteria

- [x] All Phase 1-5 tasks complete
- [x] Unit tests >90% coverage
- [x] Integration tests passing
- [x] Performance benchmarks met
- [x] Documentation complete
- [x] Legacy compatibility verified
- [x] Code review approved

