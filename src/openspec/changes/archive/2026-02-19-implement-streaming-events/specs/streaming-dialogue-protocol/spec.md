## MODIFIED Requirements
### Requirement: Event Taxonomy (Implementation)

The protocol MUST be fully implemented in the orchestrator such that all event types are emitted during appropriate orchestration phases with stable, versioned contracts.

#### Scenario: Intent Classified Event Emission
- **GIVEN** the orchestrator classifies user intent
- **WHEN** classification completes
- **THEN** an `intent.classified` event is emitted via SSE
- **AND** the event includes classification, confidence, and reasoning
- **AND** the event conforms to versioned schema v1.0

#### Scenario: Gate Selected Event Emission
- **GIVEN** intent classification indicates a workflow operation
- **WHEN** the gating engine selects a target phase
- **THEN** a `gate.selected` event is emitted via SSE
- **AND** the event includes target phase, reasoning, and requiresConfirmation flag
- **AND** the event conforms to versioned schema v1.0

#### Scenario: Phase Lifecycle Event Emission
- **GIVEN** a workflow phase executes
- **WHEN** phase starts
- **THEN** a `phase.started` event is emitted via SSE
- **WHEN** phase completes
- **THEN** a `phase.completed` event is emitted via SSE
- **AND** both events include tracing context and timing data

#### Scenario: Tool Invocation Event Emission
- **GIVEN** the orchestrator invokes a tool
- **WHEN** tool call is initiated
- **THEN** a `tool.call` event is emitted via SSE
- **WHEN** tool execution completes
- **THEN** a `tool.result` event is emitted via SSE
- **AND** events include performance metrics and error details

#### Scenario: Assistant Dialogue Event Emission
- **GIVEN** the assistant generates a response
- **WHEN** tokens are produced
- **THEN** `assistant.delta` events are emitted via SSE
- **WHEN** response completes
- **THEN** an `assistant.final` event is emitted via SSE
- **AND** events include content metadata and generation statistics

#### Scenario: Run Lifecycle Event Emission (Write Only)
- **GIVEN** a user initiates a write workflow
- **WHEN** workflow execution begins
- **THEN** a `run.started` event is emitted via SSE with run ID and tracing context
- **WHEN** workflow execution completes
- **THEN** a `run.finished` event is emitted via SSE with final status and artifacts
- **AND** NO run events are emitted for chat-only interactions

---

### Requirement: Common Event Envelope

All events MUST share a common envelope structure with validated JSON schemas and versioning support.

#### Scenario: Schema Validation
- **GIVEN** any streaming event is emitted
- **WHEN** the event passes through the event sink
- **THEN** the event payload is validated against its JSON schema
- **AND** validation failures result in error events
- **AND** schema version is included in event metadata

#### Scenario: Backward Compatibility
- **GIVEN** a client expects an older schema version
- **WHEN** events are streamed
- **THEN** events are transformed to compatible format
- **AND** newer fields are gracefully omitted

---

### Requirement: Event Sequencing

Events MUST be emitted in a logical sequence with comprehensive tracing context.

#### Scenario: Chat Response Sequence with Tracing
- **GIVEN** a chat-only interaction with correlation ID "abc-123"
- **WHEN** orchestration completes
- **THEN** events are emitted in order with tracing context:
  1. `intent.classified` (with span ID and timing)
  2. `gate.selected` (with parent span reference)
  3. `assistant.delta` (zero or more, with timing metadata)
  4. `assistant.final` (with complete timing summary)
- **AND** all events share correlation ID "abc-123"

#### Scenario: Workflow Event Sequence with Tracing
- **GIVEN** a confirmed write workflow with correlation ID "def-456"
- **WHEN** orchestration completes
- **THEN** events are emitted in order with comprehensive tracing:
  1. `intent.classified` (with trace ID and span creation)
  2. `gate.selected` (with span continuation)
  3. `run.started` (with run ID and trace context)
  4. `phase.started` (with phase span and parent reference)
  5. Zero or more (`tool.call`, `tool.result`) pairs (with tool spans)
  6. `assistant.delta` (zero or more, with generation span)
  7. `assistant.final` (with span completion)
  8. `phase.completed` (with phase span completion)
  9. `run.finished` (with run completion and trace summary)
- **AND** all events include timing, performance, and error context
