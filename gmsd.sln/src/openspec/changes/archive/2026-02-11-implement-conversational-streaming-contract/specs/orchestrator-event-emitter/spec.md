# orchestrator-event-emitter — Implementation Delta

## Purpose
This delta captures implementation requirements for the orchestrator event emitter to ensure `IStreamingOrchestrator` produces the correct event stream per Remediation.md immediate remediation #1.

---

## MODIFIED Requirements

### Requirement: IStreamingOrchestrator Implementation

The `IStreamingOrchestrator` interface MUST be fully implemented to emit events via `IAsyncEnumerable<StreamingEvent>`.

#### Scenario: ExecuteWithEventsAsync Emits Events
- **GIVEN** a `WorkflowIntent` with input and correlationId
- **WHEN** `ExecuteWithEventsAsync` is called
- **THEN** the method returns `IAsyncEnumerable<StreamingEvent>`
- **AND** events are yielded as they occur (not batched)
- **AND** the stream completes when orchestration finishes

#### Scenario: Event Sink Integration
- **GIVEN** an `IEventSink` implementation
- **WHEN** the orchestrator executes
- **THEN** all events flow through the event sink
- **AND** the streaming orchestrator adapts sink events to the async enumerable

#### Scenario: Cancellation Support
- **GIVEN** a streaming execution in progress
- **WHEN** the cancellation token is triggered
- **THEN** event emission stops gracefully
- **AND** any final error event is emitted before completion

---

### Requirement: Intent Classification Events

The orchestrator MUST emit `intent.classified` events during the classification phase.

#### Scenario: Classification Event Integration
- **GIVEN** the `InputClassifier` produces a result
- **WHEN** classification completes in the orchestrator
- **THEN** an `intent.classified` event is emitted
- **AND** the event payload matches the classification result

---

### Requirement: Gate Selection Events

The orchestrator MUST emit `gate.selected` events when the gating engine selects a phase.

#### Scenario: Gating Event Integration
- **GIVEN** the gating engine selects a target phase
- **WHEN** gate selection completes
- **THEN** a `gate.selected` event is emitted
- **AND** the event includes target phase, reasoning, and confirmation requirement

---

### Requirement: Run Lifecycle Events

The orchestrator MUST emit run lifecycle events for write operations only.

#### Scenario: Run Started Emission
- **GIVEN** a write workflow is initiated
- **WHEN** the run begins execution
- **THEN** a `run.started` event is emitted with run identifier

#### Scenario: Run Finished Emission
- **GIVEN** a run completes (success or failure)
- **WHEN** execution ends
- **THEN** a `run.finished` event is emitted
- **AND** the event includes status, duration, and artifacts

#### Scenario: Chat-Only Suppression
- **GIVEN** classification indicates Chat intent
- **WHEN** the orchestrator responds conversationally
- **THEN** NO `run.started` or `run.finished` events are emitted

---

## ADDED Requirements

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
