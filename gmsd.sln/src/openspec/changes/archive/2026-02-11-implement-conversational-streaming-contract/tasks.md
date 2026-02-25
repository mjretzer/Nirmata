# Tasks: Implement Conversational Streaming Contract

## Overview
Implementation tasks for making the orchestrator emit typed conversational events per `streaming-dialogue-protocol` spec.

---

## Phase 1: Core Event Infrastructure

### [x] Task 1.1: Verify StreamingEvent Model Coverage
**Description:** Ensure all event types from the spec are represented in the model.
**Files:** `Gmsd.Web/Models/Streaming/StreamingEvent.cs`
**Validation:** All event types (`intent.classified`, `gate.selected`, `phase.started`, `phase.completed`, `tool.call`, `tool.result`, `assistant.delta`, `assistant.final`, `run.started`, `run.finished`, `error`) have corresponding payload classes.

### [x] Task 1.2: Verify IEventSink Interface
**Description:** Ensure `IEventSink` supports all required emission patterns.
**Files:** `Gmsd.Agents/Execution/Context/IEventSink.cs` (or equivalent)
**Validation:** Interface supports typed event emission with correlation ID and sequence numbers.

### [x] Task 1.3: Verify LegacyEventAdapter Completeness
**Description:** Ensure `LegacyEventAdapter.Transform()` handles all new event types.
**Files:** `Gmsd.Web/Models/Streaming/LegacyEventAdapter.cs`
**Validation:** All `StreamingEventType` values have corresponding legacy transformation logic.

---

## Phase 2: Orchestrator Event Integration

### [x] Task 2.1: Intent Classification Event Emission
**Description:** Wire up `intent.classified` event emission in the orchestrator.
**Depends On:** Task 1.1
**Files:** `Gmsd.Agents/Execution/ControlPlane/StreamingOrchestrator.cs`
**Validation:** 
- Event emitted after `InputClassifier.Classify()` returns
- Payload contains classification, confidence, and reasoning
- Correlation ID matches request

### [x] Task 2.2: Gate Selection Event Emission
**Description:** Wire up `gate.selected` event emission in the orchestrator.
**Depends On:** Task 2.1
**Files:** `Gmsd.Agents/Execution/ControlPlane/StreamingOrchestrator.cs`
**Validation:**
- Event emitted after gating engine selects phase
- Payload includes target phase, reasoning, requiresConfirmation
- Proposed action included when applicable

### [x] Task 2.3: Phase Lifecycle Event Emission
**Description:** Wire up `phase.started` and `phase.completed` events.
**Depends On:** Task 2.2
**Files:** `Gmsd.Agents/Execution/ControlPlane/StreamingOrchestrator.cs`
**Validation:**
- `phase.started` emitted before phase execution
- `phase.completed` emitted after phase execution
- Events include phase name, run ID (if applicable), and status

### [x] Task 2.4: Run Lifecycle Event Emission
**Description:** Wire up `run.started` and `run.finished` events (write ops only).
**Depends On:** Task 2.3
**Files:** `Gmsd.Agents/Execution/ControlPlane/StreamingOrchestrator.cs`
**Validation:**
- `run.started` emitted only for write operations
- `run.finished` emitted only for write operations
- Events suppressed for chat-only interactions
- Payload includes run ID, status, duration, artifacts

### [x] Task 2.5: Assistant Dialogue Event Emission
**Description:** Wire up `assistant.delta` and `assistant.final` events.
**Depends On:** Task 2.2
**Files:** `Gmsd.Agents/Execution/ControlPlane/StreamingOrchestrator.cs`
**Validation:**
- `assistant.delta` emitted for each token chunk during streaming
- `assistant.final` emitted when response completes
- Events share the same `messageId`
- Structured data included in final event when applicable

### [x] Task 2.6: Tool Event Integration
**Description:** Ensure `StreamingToolEventSink` properly bridges tool events.
**Files:** `Gmsd.Web/AgentRunner/StreamingToolEventSink.cs`
**Validation:**
- `EmitToolCall` produces `tool.call` events
- `EmitToolResult` produces `tool.result` events
- Call IDs correlate between call and result events

---

## Phase 3: Error Handling

### [x] Task 3.1: Error Event Emission
**Description:** Ensure errors are emitted as typed `error` events.
**Files:** `Gmsd.Web/Models/Streaming/StreamingOrchestrator.cs`
**Validation:**
- `error` event emitted on exceptions
- Event includes error code, message, recoverable flag
- `phase.completed` with success=false follows error
- `run.finished` follows for write operations

---

## Phase 4: Validation & Testing

### [x] Task 4.1: Integration Test - Chat Sequence
**Description:** Test chat-only interaction event sequence.
**Files:** `tests/Gmsd.Agents.Tests/E2E/StreamingDialogueTests.cs` (create if needed)
**Validation:**
- Sequence: `intent.classified` → `gate.selected` → `assistant.delta` → `assistant.final`
- No `run.started` or `run.finished` events
- All events have matching correlation ID

### [x] Task 4.2: Integration Test - Workflow Sequence
**Description:** Test write workflow event sequence.
**Depends On:** Task 4.1
**Files:** `tests/Gmsd.Agents.Tests/E2E/StreamingDialogueTests.cs`
**Validation:**
- Sequence: `intent.classified` → `gate.selected` → `run.started` → `phase.started` → tools → assistant → `phase.completed` → `run.finished`
- All events have matching correlation ID
- Timestamps increase monotonically

### [x] Task 4.3: Legacy Endpoint Backward Compatibility
**Description:** Verify legacy endpoint continues to function.
**Files:** `tests/Gmsd.Web.Tests/ChatStreamingControllerTests.cs` (create if needed)
**Validation:**
- Legacy endpoint returns `StreamingChatEvent` format
- Event types transformed correctly (`message_start`, `thinking`, `content_chunk`, `message_complete`)
- New typed events NOT emitted to legacy clients

### [x] Task 4.4: Cancellation Support
**Description:** Verify graceful cancellation handling.
**Files:** `tests/Gmsd.Agents.Tests/E2E/StreamingDialogueTests.cs`
**Validation:**
- Cancellation token stops event emission
- Stream completes gracefully
- No orphaned events after cancellation

---

## Phase 5: Documentation

### [x] Task 5.1: Event Contract Documentation
**Description:** Document the SSE event contract for frontend developers.
**Files:** `docs/streaming-events.md` (create)
**Validation:**
- All event types documented with examples
- Event sequencing diagrams included
- Migration guide from legacy to v2 format

### [x] Task 5.2: API Endpoint Documentation
**Description:** Document the v2 streaming endpoint.
**Files:** `Gmsd.Web/Controllers/ChatStreamingController.cs` (XML comments)
**Validation:**
- `POST /api/chat/stream-v2` documented
- Accept header negotiation explained
- Event format documented

---

## Verification Checklist

Before marking this change complete:

- [x] All spec requirements from `streaming-dialogue-protocol` are implemented
- [x] All spec requirements from `orchestrator-event-emitter` are implemented
- [x] Integration tests pass for chat sequence
- [x] Integration tests pass for workflow sequence
- [x] Legacy endpoint maintains backward compatibility
- [x] `openspec validate implement-conversational-streaming-contract --strict` passes
- [x] Manual testing via `POST /api/chat/stream-v2` shows correct events

---

## Dependencies

**External:**
- `redesign-ui-chat-forward` - UI rendering of these events (parallelizable)

**Internal:**
- `InputClassifier` must be functional
- `IStreamingOrchestrator` must exist
- `LegacyEventAdapter` must exist

## Parallelizable Work

- Tasks 2.1-2.6 can be done in parallel after Task 1.x complete
- Task 3.1 can be done in parallel with Task 2.x
- Task 4.x tests can be written in parallel with implementation
