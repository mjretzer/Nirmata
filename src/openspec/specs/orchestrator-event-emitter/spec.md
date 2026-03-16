# orchestrator-event-emitter Specification

## Purpose

Defines the durable contract for $capabilityId in the nirmata platform.

- **Lives in:** See repo projects and `.aos/**` artifacts as applicable
- **Owns:** Capability-level contract and scenarios
- **Does not own:** Unrelated domain concerns outside this capability
## Requirements
### Requirement: IEventSink Interface

The system MUST provide an enhanced event sink interface with tracing capabilities and performance monitoring.

#### Scenario: Tracing-Enabled Event Sink
- **GIVEN** a streaming orchestration session with correlation ID
- **WHEN** the orchestrator begins execution
- **THEN** an `IEventSink` is available with tracing context
- **AND** the event sink captures timing and metadata for all events

#### Scenario: Null Event Sink with Tracing
- **GIVEN** a non-streaming orchestration context
- **WHEN** no event sink is provided
- **THEN** a null/no-op event sink is used
- **AND** tracing context is still maintained internally
- **AND** orchestrator logic executes without event emission

---

### Requirement: IStreamingOrchestrator Implementation

The `IStreamingOrchestrator` interface MUST be fully implemented to emit events with comprehensive tracing context.

#### Scenario: ExecuteWithEventsAsync with Tracing
- **GIVEN** a `WorkflowIntent` with input and correlationId
- **WHEN** `ExecuteWithEventsAsync` is called
- **THEN** the method returns `IAsyncEnumerable<StreamingEvent>` with tracing context
- **AND** events are yielded as they occur with span information
- **AND** the stream includes timing and performance metadata
- **AND** trace context is propagated to all components

#### Scenario: Event Sink Integration with Tracing
- **GIVEN** an `IEventSink` implementation with tracing
- **WHEN** the orchestrator executes
- **THEN** all events flow through the event sink with trace context
- **AND** the streaming orchestrator adapts sink events with span metadata
- **AND** performance metrics are captured and emitted

#### Scenario: Cancellation Support with Trace Cleanup
- **GIVEN** a streaming execution in progress with active spans
- **WHEN** the cancellation token is triggered
- **THEN** event emission stops gracefully
- **AND** active tracing spans are properly closed
- **AND** cancellation trace events are emitted

---

### Requirement: Intent Classification Events

The orchestrator MUST emit `intent.classified` events with comprehensive tracing context during the classification phase.

#### Scenario: Classification Events with Tracing
- **GIVEN** the `InputClassifier` produces a result
- **WHEN** classification completes in the orchestrator
- **THEN** an `intent.classified` event is emitted with tracing context
- **AND** the event includes classification span timing and metadata
- **AND** trace parent/child relationships are established

---

### Requirement: Gate Selection Events

The orchestrator MUST emit `gate.selected` events with comprehensive tracing context when the gating engine selects a phase.

#### Scenario: Gating Events with Tracing
- **GIVEN** the gating engine selects a target phase
- **WHEN** gate selection completes
- **THEN** a `gate.selected` event is emitted with tracing context
- **AND** the event includes gating span timing and decision metrics
- **AND** span references to classification are maintained

---

### Requirement: Run Lifecycle Events

The orchestrator MUST emit run lifecycle events with comprehensive tracing for write operations.

#### Scenario: Enhanced Run Started Emission
- **GIVEN** a write workflow is initiated with correlation ID
- **WHEN** the run begins execution
- **THEN** a `run.started` event is emitted with run identifier and trace context
- **AND** the event includes trace ID, span hierarchy, and start timing
- **AND** run-level tracing context is established

#### Scenario: Enhanced Run Finished Emission
- **GIVEN** a run completes (success or failure) with active tracing
- **WHEN** execution ends
- **THEN** a `run.finished` event is emitted with comprehensive trace summary
- **AND** the event includes final status, duration, artifacts, and trace analytics
- **AND** all active spans are properly closed

#### Scenario: Chat-Only Tracing Suppression
- **GIVEN** classification indicates Chat intent
- **WHEN** the orchestrator responds conversationally
- **THEN** NO `run.started` or `run.finished` events are emitted
- **AND** tracing remains at conversation level without run hierarchy

---

### Requirement: Phase Lifecycle Events

The orchestrator MUST emit phase lifecycle events during workflow execution.

#### Scenario: Phase Start Emission
**Given** a phase begins execution
**When** phase execution starts
**Then** a `phase.started` event is emitted with:
- Phase name
- Run identifier (if applicable)
- Context data

#### Scenario: Phase Complete Emission
**Given** a phase completes execution
**When** phase execution ends
**Then** a `phase.completed` event is emitted with:
- Phase name
- Completion status
- Output artifacts

### Requirement: Tool Call Events

The orchestrator MUST emit enhanced tool events with performance monitoring and error tracking.

#### Scenario: Enhanced Tool Call Emission
- **GIVEN** a phase invokes a tool with tracing context
- **WHEN** tool execution is initiated
- **THEN** a `tool.call` event is emitted with tool span creation
- **AND** the event includes call ID, tool name, arguments, and start timing
- **AND** tool-level tracing context is established

#### Scenario: Enhanced Tool Result Emission
- **GIVEN** a tool execution completes with active tool span
- **WHEN** result is available
- **THEN** a `tool.result` event is emitted with span completion
- **AND** the event includes call identifier, success status, result data, execution duration, and error details
- **AND** tool span is properly closed with performance metrics

#### Scenario: Tool Sequence with Tracing
- **GIVEN** a phase uses multiple tools in sequence
- **WHEN** tools execute sequentially with tracing
- **THEN** `tool.call` and `tool.result` events are emitted with span hierarchy
- **AND** call identifiers enable correlation with parent span references
- **AND** overall phase timing includes all tool execution times

### Requirement: Assistant Dialogue Events

The orchestrator MUST emit assistant dialogue events for conversational responses.

#### Scenario: Assistant Delta Emission
**Given** an LLM generates a streaming response
**When** tokens are produced
**Then** `assistant.delta` events are emitted for each token chunk
**And** all deltas share the same `messageId`

#### Scenario: Assistant Final Emission
**Given** an LLM completes response generation
**When** the final token is received
**Then** an `assistant.final` event is emitted with:
- Complete message content
- Message identifier (matching deltas)
- Optional structured data

#### Scenario: Non-Streaming Assistant Response
**Given** an LLM response without streaming
**When** complete response is available
**Then** a single `assistant.final` event is emitted
**And** no `assistant.delta` events are emitted

### Requirement: Event Ordering

Events MUST be emitted in a sequence that reflects actual orchestration flow.

#### Scenario: Chat Response Sequence
**Given** a chat-only interaction
**When** orchestration completes
**Then** event order is:
1. `intent.classified` (Chat)
2. `gate.selected` (Responder)
3. `assistant.delta` (zero or more)
4. `assistant.final`

#### Scenario: Workflow Execution Sequence
**Given** a confirmed write workflow
**When** orchestration completes
**Then** event order is:
1. `intent.classified`
2. `gate.selected`
3. `run.started`
4. `phase.started`
5. Zero or more (`tool.call`, `tool.result`) pairs
6. `assistant.delta` (zero or more)
7. `assistant.final`
8. `phase.completed`
9. `run.finished`

### Requirement: Error Event Emission

The orchestrator MUST emit error events when failures occur.

#### Scenario: Classification Error
**Given** an error during intent classification
**When** exception is caught
**Then** an `error` event is emitted with:
- Error code
- Error message
- Recoverable flag
- Context (phase where error occurred)

#### Scenario: Phase Execution Error
**Given** an error during phase execution
**When** exception is caught
**Then** an `error` event is emitted
**And** `phase.completed` is emitted with success=false
**And** `run.finished` is emitted (if write operation)

### Requirement: Legacy Event Adapter

The system MUST provide a legacy adapter for backward compatibility.

#### Scenario: Legacy Format Transformation
- **GIVEN** a new typed `StreamingEvent`
- **WHEN** the legacy adapter transforms it
- **THEN** a `StreamingChatEvent` is produced
- **AND** legacy clients receive compatible format

#### Scenario: Legacy Endpoint Support
- **GIVEN** a request to the legacy `/api/chat/stream` endpoint
- **WHEN** the streaming orchestrator executes
- **THEN** events are transformed via `LegacyEventAdapter`
- **AND** the response format matches the original spec

---

### Requirement: Tool Call Integration

Tool calls MUST emit `tool.call` and `tool.result` events via `IToolEventSink`.

#### Scenario: StreamingToolEventSink Bridge
- **GIVEN** `StreamingToolEventSink` is configured
- **WHEN** a tool is invoked
- **THEN** `tool.call` event is emitted via the event sink
- **WHEN** tool execution completes
- **THEN** `tool.result` event is emitted via the event sink

---

