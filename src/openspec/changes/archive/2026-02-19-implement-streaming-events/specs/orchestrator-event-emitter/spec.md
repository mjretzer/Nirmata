## MODIFIED Requirements
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
