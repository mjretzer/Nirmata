# Event Type Reference Guide

## Overview

The streaming dialogue protocol defines a taxonomy of event types that represent the agent's reasoning, operations, and conversational output. This guide provides detailed specifications for each event type, including payload schemas, timing, and rendering guidance.

## Event Categories

Events are organized into four categories based on their purpose:

| Category | Description | Visual Treatment |
|----------|-------------|------------------|
| **Reasoning** | Agent's decision process | Collapsible blocks, muted styling |
| **Operation** | Work being performed | Cards, progress indicators |
| **Dialogue** | Conversational content | Message bubbles, streaming text |
| **Error** | Error conditions | Alert banners, error styling |

---

## Common Event Structure

All events share a common envelope structure:

```typescript
interface StreamingEvent {
  id: string;              // UUID for deduplication
  type: StreamingEventType; // Event discriminator
  timestamp: string;       // ISO 8601
  correlationId: string;  // Links events in conversation
  sequenceNumber?: number; // Ordering hint
  payload: unknown;         // Type-specific data
}
```

---

## Reasoning Events

### IntentClassified

**Purpose:** Emitted when the orchestrator classifies user intent.

**When:** Immediately after classification, before gating.

**Payload Schema:**

```typescript
interface IntentClassifiedPayload {
  classification: 'Chat' | 'ReadOnly' | 'Write';
  confidence: number;      // 0.0 - 1.0
  reasoning: string;       // LLM explanation
}
```

**Example:**

```json
{
  "id": "event-001",
  "type": "IntentClassified",
  "timestamp": "2026-02-11T14:30:00Z",
  "correlationId": "thread-123",
  "sequenceNumber": 1,
  "payload": {
    "classification": "Write",
    "confidence": 0.92,
    "reasoning": "User wants to create a plan, which requires write access to workspace and will result in a new run being created."
  }
}
```

**UI Treatment:**
- Collapsible reasoning block with light styling
- Show confidence as visual indicator (bar or badge)
- Color-code: Chat (blue), ReadOnly (green), Write (orange)

**Edge Cases:**
- Low confidence (< 0.6): Show warning indicator
- Classification error: Followed by Error event

---

### GateSelected

**Purpose:** Emitted when the gating engine selects a target phase.

**When:** After classification, before phase execution.

**Payload Schema:**

```typescript
interface GateSelectedPayload {
  targetPhase: string;           // e.g., "Planner", "Executor"
  reasoning: string;             // Selection explanation
  requiresConfirmation: boolean; // User confirmation needed
  proposedAction?: {
    name: string;
    description: string;
    parameters?: Record<string, unknown>;
  };
}
```

**Example (no confirmation):**

```json
{
  "id": "event-002",
  "type": "GateSelected",
  "timestamp": "2026-02-11T14:30:01Z",
  "correlationId": "thread-123",
  "sequenceNumber": 2,
  "payload": {
    "targetPhase": "Planner",
    "reasoning": "Input matches planning intent pattern with clear scope specification",
    "requiresConfirmation": false
  }
}
```

**Example (confirmation required):**

```json
{
  "id": "event-002",
  "type": "GateSelected",
  "timestamp": "2026-02-11T14:30:01Z",
  "correlationId": "thread-123",
  "sequenceNumber": 2,
  "payload": {
    "targetPhase": "Executor",
    "reasoning": "User is requesting a destructive operation",
    "requiresConfirmation": true,
    "proposedAction": {
      "name": "delete_spec",
      "description": "Delete the foundation phase specification",
      "parameters": { "specId": "spec-123" }
    }
  }
}
```

**UI Treatment:**
- Decision card with target phase badge
- Confirmation button when `requiresConfirmation` is true
- Cancel/abort action available

---

## Operation Events

### RunLifecycle

**Purpose:** Marks the start and end of a run (write operation).

**When:** 
- `started`: When write workflow begins
- `finished`: When workflow completes (success/failure)

**Payload Schema:**

```typescript
interface RunLifecyclePayload {
  status: 'started' | 'finished';
  runId: string;
  duration?: string;              // ISO 8601 duration
  artifactReferences?: {
    type: string;
    id: string;
    name: string;
  }[];
}
```

**Example (started):**

```json
{
  "id": "event-003",
  "type": "RunLifecycle",
  "timestamp": "2026-02-11T14:30:02Z",
  "correlationId": "thread-123",
  "sequenceNumber": 3,
  "payload": {
    "status": "started",
    "runId": "RUN-2024-001"
  }
}
```

**Example (finished):**

```json
{
  "id": "event-010",
  "type": "RunLifecycle",
  "timestamp": "2026-02-11T14:32:30Z",
  "correlationId": "thread-123",
  "sequenceNumber": 10,
  "payload": {
    "status": "finished",
    "runId": "RUN-2024-001",
    "duration": "PT2M28S",
    "artifactReferences": [
      { "type": "PhasePlan", "id": "plan-001", "name": "Foundation Phase Plan" }
    ]
  }
}
```

**UI Treatment:**
- Status indicator in sidebar or header
- Link to run details when finished
- Artifact list with download/view links

**Note:** Suppressed in `chatOnly` mode.

---

### PhaseLifecycle

**Purpose:** Tracks phase execution progress.

**When:**
- `started`: Phase handler begins
- `completed`: Phase handler finishes

**Payload Schema:**

```typescript
interface PhaseLifecyclePayload {
  status: 'started' | 'completed';
  phase: string;                    // Phase name
  runId?: string;                   // Associated run
  context?: Record<string, unknown>; // Input context
  outputArtifacts?: {
    type: string;
    id: string;
    name: string;
  }[];
}
```

**Example:**

```json
{
  "id": "event-004",
  "type": "PhaseLifecycle",
  "timestamp": "2026-02-11T14:30:03Z",
  "correlationId": "thread-123",
  "sequenceNumber": 4,
  "payload": {
    "status": "started",
    "phase": "Planner",
    "runId": "RUN-2024-001",
    "context": { "intent": "plan the foundation phase" }
  }
}
```

**UI Treatment:**
- Timeline indicator in sidebar
- Expandable for phase details
- Tool calls nested under phase

---

### ToolCall

**Purpose:** Announces a tool invocation.

**When:** Before tool execution begins.

**Payload Schema:**

```typescript
interface ToolCallPayload {
  toolName: string;
  arguments: Record<string, unknown>;
  callId: string;          // Unique per call
}
```

**Example:**

```json
{
  "id": "event-005",
  "type": "ToolCall",
  "timestamp": "2026-02-11T14:30:05Z",
  "correlationId": "thread-123",
  "sequenceNumber": 5,
  "payload": {
    "toolName": "read_workspace_specs",
    "arguments": { "scope": "foundation", "includeDrafts": false },
    "callId": "call-001"
  }
}
```

**UI Treatment:**
- Card with tool name and pending spinner
- Collapsible argument display
- Correlated with subsequent ToolResult

---

### ToolResult

**Purpose:** Reports tool execution outcome.

**When:** After tool execution completes.

**Payload Schema:**

```typescript
interface ToolResultPayload {
  callId: string;          // Matches ToolCall
  success: boolean;
  result: unknown;         // Tool-specific output
  durationMs: number;
}
```

**Example (success):**

```json
{
  "id": "event-006",
  "type": "ToolResult",
  "timestamp": "2026-02-11T14:30:06Z",
  "correlationId": "thread-123",
  "sequenceNumber": 6,
  "payload": {
    "callId": "call-001",
    "success": true,
    "result": { "specs": [{ "id": "spec-1", "name": "Foundation" }] },
    "durationMs": 145
  }
}
```

**Example (failure):**

```json
{
  "id": "event-006",
  "type": "ToolResult",
  "timestamp": "2026-02-11T14:30:06Z",
  "correlationId": "thread-123",
  "sequenceNumber": 6,
  "payload": {
    "callId": "call-002",
    "success": false,
    "result": { "error": "Spec not found", "code": "NOT_FOUND" },
    "durationMs": 23
  }
}
```

**UI Treatment:**
- Updates existing ToolCall card
- Success: Green checkmark
- Failure: Red warning with retry option
- Collapsible result display

---

## Dialogue Events

### AssistantDelta

**Purpose:** Streams assistant response tokens in real-time.

**When:** During LLM streaming generation.

**Payload Schema:**

```typescript
interface AssistantDeltaPayload {
  content: string;         // Token chunk (may be partial word)
  messageId: string;     // Groups chunks into message
}
```

**Example:**

```json
{
  "id": "event-007",
  "type": "AssistantDelta",
  "timestamp": "2026-02-11T14:30:10Z",
  "correlationId": "thread-123",
  "sequenceNumber": 7,
  "payload": {
    "content": "I've ",
    "messageId": "msg-001"
  }
}
```

**UI Treatment:**
- Append to message buffer
- Render with typing indicator
- Coalesce rapid deltas (< 16ms apart)

---

### AssistantFinal

**Purpose:** Finalizes assistant message with complete content.

**When:** After all deltas streamed or for non-streaming responses.

**Payload Schema:**

```typescript
interface AssistantFinalPayload {
  messageId: string;     // Matches delta events
  content: string;       // Complete message
  structuredData?: unknown; // Optional parsed artifacts
}
```

**Example:**

```json
{
  "id": "event-008",
  "type": "AssistantFinal",
  "timestamp": "2026-02-11T14:30:15Z",
  "correlationId": "thread-123",
  "sequenceNumber": 8,
  "payload": {
    "messageId": "msg-001",
    "content": "I've analyzed the foundation phase requirements and created a plan with 5 tasks.",
    "structuredData": {
      "planId": "plan-001",
      "taskCount": 5,
      "tasks": ["Task 1", "Task 2", "Task 3", "Task 4", "Task 5"]
    }
  }
}
```

**UI Treatment:**
- Replace streaming text with formatted content
- Render structured data (tables, cards, code)
- Enable copy/quote actions

---

## Error Events

### Error

**Purpose:** Reports error conditions during execution.

**When:** Anytime an error occurs.

**Payload Schema:**

```typescript
interface ErrorPayload {
  code: string;           // Error code (e.g., "CLASSIFICATION_FAILED")
  message: string;        // Human-readable message
  phase?: string;         // Phase context
  context?: string;       // General context (alternative to phase)
  severity?: 'error' | 'warning' | 'info';
  recoverable?: boolean;  // Can retry?
  retryAction?: string;   // Action identifier for retry
  eventId?: string;       // Event ID for retry correlation
}
```

**Example (recoverable):**

```json
{
  "id": "event-err",
  "type": "Error",
  "timestamp": "2026-02-11T14:30:20Z",
  "correlationId": "thread-123",
  "sequenceNumber": 9,
  "payload": {
    "code": "TOOL_TIMEOUT",
    "message": "Tool execution timed out after 30 seconds",
    "phase": "Execution",
    "severity": "warning",
    "recoverable": true,
    "retryAction": "retry_tool",
    "eventId": "event-005"
  }
}
```

**Example (fatal):**

```json
{
  "id": "event-err",
  "type": "Error",
  "timestamp": "2026-02-11T14:30:20Z",
  "correlationId": "thread-123",
  "sequenceNumber": 9,
  "payload": {
    "code": "ORCHESTRATION_FAILED",
    "message": "Orchestrator encountered an unrecoverable error",
    "severity": "error",
    "recoverable": false
  }
}
```

**UI Treatment:**
- Severity-based styling (red/orange/blue)
- Retry button for recoverable errors
- Link to error details/documentation

---

## Event Sequences

### Chat-Only Flow

For conversational queries without side effects:

```
IntentClassified (Chat) → AssistantDelta* → AssistantFinal
```

### Write Workflow Flow

For operations that create/modify data:

```
IntentClassified (Write) → GateSelected → RunLifecycle(started) 
→ PhaseLifecycle(started) → ToolCall → ToolResult 
→ AssistantDelta* → AssistantFinal → PhaseLifecycle(completed) 
→ RunLifecycle(finished)
```

### Multi-Phase Flow

Complex workflows with multiple phases:

```
IntentClassified → GateSelected → RunLifecycle(started)
→ PhaseLifecycle(started:Planning) → ToolCall* → ToolResult* 
→ PhaseLifecycle(completed:Planning)
→ PhaseLifecycle(started:Execution) → ToolCall* → ToolResult*
→ PhaseLifecycle(completed:Execution)
→ RunLifecycle(finished)
```

---

## Correlation and Ordering

### Event Correlation

All events in a single request share the same `correlationId`:

```
correlationId: "thread-abc-123"
├── IntentClassified (seq: 1)
├── GateSelected (seq: 2)
├── RunLifecycle (seq: 3)
└── ...
```

### Message Correlation

Assistant deltas and final share a `messageId`:

```
messageId: "msg-xyz-789"
├── AssistantDelta: "I've "
├── AssistantDelta: "analyzed "
├── AssistantDelta: "the..."
└── AssistantFinal: "I've analyzed the requirements..."
```

### Tool Correlation

Tool calls and results are linked via `callId`:

```
callId: "call-001"
├── ToolCall (pending state)
└── ToolResult (updates call card)
```

---

## JSON Schema

Complete JSON Schema available at:

```
nirmata.Web/Models/Streaming/StreamingEventSchema.json
```

The schema validates:
- Required fields (id, type, timestamp, payload)
- Type-specific payload structure
- Enum values (event types, severities, statuses)

---

## See Also

- [API Documentation](./API_DOCUMENTATION.md)
- [UI Renderer Development Guide](./UI_RENDERER_DEVELOPMENT_GUIDE.md)
- [Migration Guide](./MIGRATION_GUIDE.md)
