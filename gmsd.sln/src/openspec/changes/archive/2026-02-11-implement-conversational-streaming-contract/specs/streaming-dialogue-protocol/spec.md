# streaming-dialogue-protocol — Implementation Delta

## Purpose
This delta captures implementation requirements for the streaming dialogue protocol to make the orchestrator emit typed conversational events per Remediation.md immediate remediation #1.

---

## MODIFIED Requirements

### Requirement: Event Taxonomy (Implementation)

The protocol MUST be fully implemented in the orchestrator such that all event types are emitted during appropriate orchestration phases.

#### Scenario: Intent Classified Event Emission
- **GIVEN** the orchestrator classifies user intent
- **WHEN** classification completes
- **THEN** an `intent.classified` event is emitted via SSE
- **AND** the event includes classification, confidence, and reasoning

#### Scenario: Gate Selected Event Emission
- **GIVEN** intent classification indicates a workflow operation
- **WHEN** the gating engine selects a target phase
- **THEN** a `gate.selected` event is emitted via SSE
- **AND** the event includes target phase, reasoning, and requiresConfirmation flag

#### Scenario: Phase Lifecycle Event Emission
- **GIVEN** a workflow phase executes
- **WHEN** phase starts
- **THEN** a `phase.started` event is emitted via SSE
- **WHEN** phase completes
- **THEN** a `phase.completed` event is emitted via SSE

#### Scenario: Tool Invocation Event Emission
- **GIVEN** the orchestrator invokes a tool
- **WHEN** tool call is initiated
- **THEN** a `tool.call` event is emitted via SSE
- **WHEN** tool execution completes
- **THEN** a `tool.result` event is emitted via SSE

#### Scenario: Assistant Dialogue Event Emission
- **GIVEN** the assistant generates a response
- **WHEN** tokens are produced
- **THEN** `assistant.delta` events are emitted via SSE
- **WHEN** response completes
- **THEN** an `assistant.final` event is emitted via SSE

#### Scenario: Run Lifecycle Event Emission (Write Only)
- **GIVEN** a user initiates a write workflow
- **WHEN** workflow execution begins
- **THEN** a `run.started` event is emitted via SSE
- **WHEN** workflow execution completes
- **THEN** a `run.finished` event is emitted via SSE
- **AND** NO run events are emitted for chat-only interactions

---

## ADDED Requirements

### Requirement: Event Sequencing Guarantee

Events MUST be emitted in the correct sequence reflecting actual orchestration flow.

#### Scenario: Chat-Only Event Sequence
- **GIVEN** a chat-only interaction
- **WHEN** orchestration completes
- **THEN** events are emitted in order:
  1. `intent.classified`
  2. `gate.selected`
  3. `assistant.delta` (zero or more)
  4. `assistant.final`
- **AND** NO `run.started` or `run.finished` events appear

#### Scenario: Workflow Event Sequence
- **GIVEN** a confirmed write workflow
- **WHEN** orchestration completes
- **THEN** events are emitted in order:
  1. `intent.classified`
  2. `gate.selected`
  3. `run.started`
  4. `phase.started`
  5. Zero or more (`tool.call`, `tool.result`) pairs
  6. `assistant.delta` (zero or more)
  7. `assistant.final`
  8. `phase.completed`
  9. `run.finished`

---

### Requirement: Correlation ID Propagation

All events in a single request MUST share the same correlation ID.

#### Scenario: Correlation ID Consistency
- **GIVEN** a streaming request with correlationId "abc-123"
- **WHEN** events are emitted throughout orchestration
- **THEN** every event contains correlationId "abc-123"
- **AND** events can be traced to the originating request

---

### Requirement: Timestamp Accuracy

Event timestamps MUST reflect actual emission time in UTC.

#### Scenario: Timestamp Validation
- **GIVEN** any streaming event
- **WHEN** the event is emitted
- **THEN** the timestamp field contains ISO 8601 UTC timestamp
- **AND** timestamp reflects actual emission time (not creation time)

---
