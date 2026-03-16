# streaming-dialogue-protocol Specification

## Purpose

Defines the durable contract for $capabilityId in the nirmata platform.

- **Lives in:** See repo projects and `.aos/**` artifacts as applicable
- **Owns:** Capability-level contract and scenarios
- **Does not own:** Unrelated domain concerns outside this capability
## Requirements
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

### Requirement: Operation Events

The protocol MUST emit events for workflow lifecycle and tool operations.

#### Scenario: Phase Lifecycle
**Given** a workflow phase executes
**When** phase starts
**Then** a `phase.started` event is emitted
**When** phase completes
**Then** a `phase.completed` event is emitted

**Event Structures**:
```json
// phase.started
{
  "type": "phase.started",
  "payload": {
    "phase": "Planner",
    "runId": "RUN-2024-001",
    "context": { "phaseId": "PH-0001", "projectId": "PROJ-001" }
  }
}

// phase.completed
{
  "type": "phase.completed",
  "payload": {
    "phase": "Planner",
    "runId": "RUN-2024-001",
    "success": true,
    "artifacts": ["plan.json", "tasks.json"]
  }
}
```

#### Scenario: Tool Invocation
**Given** the orchestrator invokes a tool
**When** tool call is initiated
**Then** a `tool.call` event is emitted
**When** tool execution completes
**Then** a `tool.result` event is emitted

**Event Structures**:
```json
// tool.call
{
  "type": "tool.call",
  "payload": {
    "callId": "call-uuid-001",
    "toolName": "read_workspace_specs",
    "arguments": { "scope": "foundation" }
  }
}

// tool.result
{
  "type": "tool.result",
  "payload": {
    "callId": "call-uuid-001",
    "success": true,
    "result": { "specs": [...] },
    "durationMs": 45
  }
}
```

### Requirement: Dialogue Events

The protocol MUST support streaming assistant responses token-by-token.

#### Scenario: Streaming Assistant Response
**Given** the assistant generates a response
**When** tokens are produced
**Then** `assistant.delta` events are emitted with token chunks
**When** response completes
**Then** an `assistant.final` event is emitted with complete message

**Event Structures**:
```json
// assistant.delta
{
  "type": "assistant.delta",
  "payload": {
    "messageId": "msg-uuid-001",
    "content": "I've analyzed"
  }
}

// assistant.final
{
  "type": "assistant.final",
  "payload": {
    "messageId": "msg-uuid-001",
    "content": "I've analyzed the foundation phase requirements...",
    "structuredData": { "planId": "PLAN-001", "taskCount": 5 }
  }
}
```

### Requirement: Run Lifecycle Events

Run lifecycle events MUST only be emitted for actual write operations.

#### Scenario: Chat-Only Interaction
**Given** a user sends a chat message without workflow intent
**When** classification indicates "Chat"
**Then** NO `run.started` or `run.finished` events are emitted

#### Scenario: Workflow Execution
**Given** a user initiates a write workflow
**When** workflow execution begins
**Then** a `run.started` event is emitted
**When** workflow execution completes
**Then** a `run.finished` event is emitted

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

### Requirement: Error Handling

Errors MUST be emitted as typed error events with context.

#### Scenario: Orchestration Error
**Given** an error occurs during orchestration
**When** error is caught
**Then** an `error` event is emitted

**Event Structure**:
```json
{
  "type": "error",
  "payload": {
    "code": "ORCHESTRATOR_ERROR",
    "message": "Failed to classify intent",
    "recoverable": false,
    "context": { "phase": "Classification" }
  }
}
```

### Requirement: Backward Compatibility

The protocol MUST maintain backward compatibility with existing clients.

#### Scenario: Legacy Client Support
**Given** a client uses the legacy endpoint
**When** events are streamed
**Then** legacy event types (`message_start`, `thinking`, `content_chunk`, `message_complete`) are emitted
**And** new typed events are NOT emitted to legacy clients

### Requirement: Gate Rejection Event

The protocol MUST support a `gate.rejected` event when users decline a proposed action.

#### Scenario: User rejects destructive operation

- **GIVEN** a destructive workflow (e.g., executing tasks that modify files)
- **WHEN** the user rejects the proposed action at the gate
- **THEN** a `gate.rejected` event is emitted

**Event Structure**:
```json
{
  "type": "gate.rejected",
  "payload": {
    "targetPhase": "Executor",
    "rejectionReason": "User declined",
    "alternativeOptions": [
      { "type": "Chat", "description": "Discuss the plan first" },
      { "type": "ReadOnly", "description": "Preview what would change" }
    ]
  }
}
```

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

