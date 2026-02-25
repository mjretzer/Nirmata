# streaming-dialogue-protocol — Specification

## Overview

Defines the Server-Sent Events (SSE) protocol for agent dialogue streaming. This specification establishes the event taxonomy, JSON schema, and sequencing rules for streaming agent reasoning, operations, and conversational content from the orchestrator to the UI.

## ADDED Requirements

### Requirement: Event Taxonomy

The protocol MUST support three categories of events: Reasoning, Operation, and Dialogue.

#### Scenario: Classification Reasoning
**Given** the orchestrator classifies user intent
**When** classification completes
**Then** an `intent.classified` event is emitted with classification result and confidence

**Event Structure**:
```json
{
  "id": "evt-uuid-001",
  "type": "intent.classified",
  "timestamp": "2026-02-10T12:00:00Z",
  "correlationId": "corr-uuid-123",
  "payload": {
    "classification": "Write",
    "confidence": 0.92,
    "reasoning": "User request contains planning verbs and implies state modification"
  }
}
```

#### Scenario: Gate Selection
**Given** intent classification indicates a workflow operation
**When** the gating engine selects a target phase
**Then** a `gate.selected` event is emitted with phase and reasoning

**Event Structure**:
```json
{
  "id": "evt-uuid-002",
  "type": "gate.selected",
  "timestamp": "2026-02-10T12:00:01Z",
  "correlationId": "corr-uuid-123",
  "payload": {
    "targetPhase": "Planner",
    "reasoning": "Input matches planning intent for foundation phase",
    "requiresConfirmation": true,
    "proposedAction": {
      "type": "CreatePhasePlan",
      "phaseId": "PH-0001",
      "description": "Generate task plan for foundation phase"
    }
  }
}
```

### Requirement: Common Event Envelope

All events MUST share a common envelope structure with mandatory fields.

#### Scenario: Event Validation
**Given** any streaming event
**Then** it MUST contain an `id` field with unique event identifier
**And** it MUST contain a `type` field with event type discriminator
**And** it MUST contain a `timestamp` field with ISO 8601 UTC timestamp
**And** it MUST contain a `correlationId` field linking to originating request
**And** it MUST contain a `payload` field with type-specific data

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

Events MUST be emitted in a logical sequence that reflects the orchestration flow.

#### Scenario: Chat Response Sequence
**Given** a chat-only interaction
**When** orchestration completes
**Then** events are emitted in order: intent.classified then gate.selected then assistant.delta then assistant.final

#### Scenario: Complete Workflow Sequence
**Given** a user initiates a planning workflow
**When** the orchestration proceeds
**Then** events are emitted in order: intent.classified then gate.selected then run.started then phase.started then tool events then assistant events then phase.completed then run.finished

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

## MODIFIED Requirements

None.

## REMOVED Requirements

None.

---

## Event Type Reference

| Event Type | Category | Description |
|------------|----------|-------------|
| `intent.classified` | Reasoning | Intent classification result with confidence |
| `gate.selected` | Reasoning | Selected phase with reasoning |
| `run.started` | Operation | Workflow run initiation |
| `run.finished` | Operation | Workflow run completion |
| `phase.started` | Operation | Phase execution start |
| `phase.completed` | Operation | Phase execution completion |
| `tool.call` | Operation | Tool invocation request |
| `tool.result` | Operation | Tool execution result |
| `assistant.delta` | Dialogue | Streaming token chunk |
| `assistant.final` | Dialogue | Complete assistant message |
| `error` | System | Error notification |

## Schema Validation

All events MUST validate against the JSON schema defined in `Gmsd.Web/Contracts/StreamingEventSchema.json`.
