# Streaming Events Documentation

**API Version:** v2  
**Protocol:** Server-Sent Events (SSE)  
**Content-Type:** `text/event-stream`  

---

## Overview

The GMSD streaming API emits typed events over Server-Sent Events (SSE) to provide real-time visibility into the orchestration flow. This document describes the event contract for the v2 streaming endpoint.

**Endpoint:** `POST /api/chat/stream-v2`

---

## Common Event Envelope

All events share a common envelope structure:

```json
{
  "id": "evt-uuid-001",
  "type": "intent.classified",
  "timestamp": "2026-02-10T12:00:00Z",
  "correlationId": "corr-uuid-123",
  "sequenceNumber": 1,
  "payload": { ... }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Unique identifier for this event |
| `type` | string | Event type discriminator (see Event Types below) |
| `timestamp` | ISO 8601 | UTC timestamp when the event was emitted |
| `correlationId` | string | Links events to the originating request |
| `sequenceNumber` | integer | Optional ordering hint for event sequencing |
| `payload` | object | Type-specific event data |

---

## Event Types

### Reasoning Events

Events that expose the orchestrator's decision-making process.

#### `intent.classified`

Emitted after the input classifier analyzes user intent.

**Payload:**
```json
{
  "category": "Write",
  "confidence": 0.92,
  "reasoning": "User request contains planning verbs and implies state modification",
  "userInput": "Create a plan for the foundation phase"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `category` | string | Classified intent: `Chat`, `Write`, `Plan`, `Execute` |
| `confidence` | number | Score between 0.0 and 1.0 |
| `reasoning` | string | Explanation for the classification decision |
| `userInput` | string | The original input that was classified |

---

#### `gate.selected`

Emitted when the gating engine selects a target phase.

**Payload:**
```json
{
  "phase": "Planner",
  "reasoning": "Input matches planning intent for foundation phase",
  "requiresConfirmation": true,
  "proposedAction": {
    "id": "action-uuid-001",
    "description": "Generate task plan for foundation phase",
    "actionType": "CreatePhasePlan",
    "parameters": { "phaseId": "PH-0001" }
  }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `phase` | string | Target phase selected by the gate |
| `reasoning` | string | Explanation for the gating decision |
| `requiresConfirmation` | boolean | Whether user confirmation is required |
| `proposedAction` | object | Details of the proposed action (if confirmation needed) |

---

### Operation Events

Events for workflow lifecycle and tool operations.

#### `phase.started`

Emitted when a workflow phase begins execution.

**Payload:**
```json
{
  "phase": "Planner",
  "status": "started",
  "context": { "phaseId": "PH-0001", "projectId": "PROJ-001" }
}
```

---

#### `phase.completed`

Emitted when a workflow phase completes (success or failure).

**Payload (Success):**
```json
{
  "phase": "Planner",
  "status": "completed",
  "context": { "phaseId": "PH-0001", "projectId": "PROJ-001" },
  "artifacts": [
    { "type": "plan", "name": "foundation-plan", "reference": "/plans/PLAN-001" }
  ]
}
```

**Payload (Failure):**
```json
{
  "phase": "Planner",
  "status": "completed",
  "context": { "phaseId": "PH-0001" },
  "error": {
    "code": "PLANNING_FAILED",
    "message": "Could not generate valid plan from input",
    "details": "Constraint validation failed for phase dependencies"
  }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `phase` | string | Phase name |
| `status` | string | `"started"` or `"completed"` |
| `context` | object | Phase-specific contextual data |
| `artifacts` | array | Output artifacts produced by the phase |
| `error` | object | Error details (if phase failed) |

---

#### `tool.call`

Emitted when a tool is invoked.

**Payload:**
```json
{
  "callId": "call-uuid-001",
  "toolName": "read_workspace_specs",
  "parameters": { "scope": "foundation" },
  "phaseContext": "Planner"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `callId` | string | Unique identifier for correlating with `tool.result` |
| `toolName` | string | Name of the tool being invoked |
| `parameters` | object | Parameters passed to the tool |
| `phaseContext` | string | Phase in which the tool is being called |

---

#### `tool.result`

Emitted when a tool execution completes.

**Payload (Success):**
```json
{
  "callId": "call-uuid-001",
  "success": true,
  "result": { "specs": [...] },
  "durationMs": 45
}
```

**Payload (Failure):**
```json
{
  "callId": "call-uuid-001",
  "success": false,
  "error": "File not found: foundation/spec.md",
  "durationMs": 12
}
```

| Field | Type | Description |
|-------|------|-------------|
| `callId` | string | Matches the `callId` from the corresponding `tool.call` |
| `success` | boolean | Whether the tool execution succeeded |
| `result` | object | Result data (if success) |
| `error` | string | Error message (if failure) |
| `durationMs` | integer | Execution duration in milliseconds |

---

#### `run.started`

Emitted when a write workflow execution begins. **Only emitted for actual write operations.**

**Payload:**
```json
{
  "status": "started",
  "runId": "RUN-2024-001"
}
```

---

#### `run.finished`

Emitted when a write workflow execution completes. **Only emitted for actual write operations.**

**Payload (Success):**
```json
{
  "status": "finished",
  "runId": "RUN-2024-001",
  "success": true,
  "durationMs": 2450,
  "artifactReferences": ["/outputs/result.json"]
}
```

**Payload (Failure):**
```json
{
  "status": "finished",
  "runId": "RUN-2024-001",
  "success": false,
  "durationMs": 1200
}
```

| Field | Type | Description |
|-------|------|-------------|
| `status` | string | `"started"` or `"finished"` |
| `runId` | string | Unique identifier for the run |
| `success` | boolean | Whether the run completed successfully (finished only) |
| `durationMs` | integer | Total run duration in milliseconds (finished only) |
| `artifactReferences` | array | References to run outputs (finished only) |

---

### Dialogue Events

Events for streaming assistant responses.

#### `assistant.delta`

Emitted for each token chunk during streaming assistant responses.

**Payload:**
```json
{
  "messageId": "msg-uuid-001",
  "content": "I've analyzed",
  "index": 0
}
```

| Field | Type | Description |
|-------|------|-------------|
| `messageId` | string | Unique identifier for the message (shared across all deltas for a single response) |
| `content` | string | Token chunk content |
| `index` | integer | Position in the overall message (0-indexed) |

---

#### `assistant.final`

Emitted when the assistant response is complete.

**Payload:**
```json
{
  "messageId": "msg-uuid-001",
  "content": "I've analyzed the foundation phase requirements and generated a comprehensive plan...",
  "structuredData": { "planId": "PLAN-001", "taskCount": 5 },
  "contentType": "application/json",
  "isFinal": true
}
```

| Field | Type | Description |
|-------|------|-------------|
| `messageId` | string | Matches the `messageId` from the delta stream |
| `content` | string | Complete final content |
| `structuredData` | object | Structured data if response contains rich content |
| `contentType` | string | MIME type of the content |
| `isFinal` | boolean | Always `true` for final events |

---

### Confirmation Events

Events for the confirmation gate lifecycle. These events enable user confirmation workflows for write-side-effect operations.

#### `confirmation.requested`

Emitted when a write operation requires user confirmation before proceeding.

**Payload:**
```json
{
  "confirmationId": "conf-abc123",
  "action": {
    "phase": "Executor",
    "description": "Create new file: src/config.json with environment settings",
    "riskLevel": "WriteSafe",
    "sideEffects": ["file.write"],
    "affectedResources": ["src/config.json"],
    "metadata": { "estimatedSize": "2KB" }
  },
  "riskLevel": "WriteSafe",
  "reason": "Confidence (0.75) below threshold (0.90)",
  "confidence": 0.75,
  "threshold": 0.90,
  "timeout": "PT5M",
  "confirmationKey": "RXhlY3V0b3I6c3JjL2NvbmZpZy5qc29u"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `confirmationId` | string | Unique identifier for this confirmation request |
| `action` | object | The proposed action requiring confirmation |
| `action.phase` | string | Target workflow phase (e.g., "Executor", "Planner") |
| `action.description` | string | Human-readable description of the action |
| `action.riskLevel` | string | Risk level: `Read`, `WriteSafe`, `WriteDestructive`, `WriteDestructiveGit`, `WorkspaceDestructive` |
| `action.affectedResources` | array | List of files/resources that will be modified |
| `riskLevel` | string | Overall risk assessment of the operation |
| `reason` | string | Explanation for why confirmation is required |
| `confidence` | number | Confidence score that triggered confirmation (0.0-1.0) |
| `threshold` | number | The threshold that was not met (0.0-1.0) |
| `timeout` | string | ISO 8601 duration (e.g., "PT5M" for 5 minutes). Null means indefinite. |
| `confirmationKey` | string | Hash key for duplicate detection |

---

#### `confirmation.accepted`

Emitted when the user accepts a confirmation request.

**Payload:**
```json
{
  "confirmationId": "conf-abc123",
  "acceptedAt": "2026-02-10T12:05:30Z",
  "action": {
    "phase": "Executor",
    "description": "Create new file: src/config.json"
  }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `confirmationId` | string | The confirmation ID from the `confirmation.requested` event |
| `acceptedAt` | ISO 8601 | Timestamp when the confirmation was accepted |
| `action` | object | The action that was accepted (subset of requested action) |

---

#### `confirmation.rejected`

Emitted when the user rejects a confirmation request.

**Payload:**
```json
{
  "confirmationId": "conf-abc123",
  "rejectedAt": "2026-02-10T12:05:30Z",
  "userMessage": "Please don't modify config files directly",
  "action": {
    "phase": "Executor",
    "description": "Create new file: src/config.json"
  }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `confirmationId` | string | The confirmation ID from the `confirmation.requested` event |
| `rejectedAt` | ISO 8601 | Timestamp when the confirmation was rejected |
| `userMessage` | string | Optional user-provided explanation for rejection |
| `action` | object | The action that was rejected |

---

#### `confirmation.timeout`

Emitted when a confirmation request expires without user response.

**Payload:**
```json
{
  "confirmationId": "conf-abc123",
  "requestedAt": "2026-02-10T12:00:00Z",
  "timeout": "PT5M",
  "action": {
    "phase": "Executor",
    "description": "Create new file: src/config.json"
  },
  "cancellationReason": "timeout",
  "message": "Confirmation timed out after 300 seconds. The operation was cancelled."
}
```

| Field | Type | Description |
|-------|------|-------------|
| `confirmationId` | string | The confirmation ID from the `confirmation.requested` event |
| `requestedAt` | ISO 8601 | When the confirmation was originally requested |
| `timeout` | string | ISO 8601 duration that was exceeded |
| `action` | object | The action that timed out |
| `cancellationReason` | string | Reason for cancellation (e.g., "timeout", "user_cancelled", "system_error") |
| `message` | string | Human-readable timeout explanation |

---

### Confirmation Event Sequences

When a write operation requires confirmation, the event sequence includes confirmation lifecycle events:

```
┌─────────────────────┐
│  intent.classified  │
│  (category: Write)  │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│   gate.selected     │
│  (requiresConfirm:  │
│       true)         │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ confirmation.requested│
│  (action details)   │
└──────────┬──────────┘
           │
           ├────────────────────┐
           │                    │
           ▼                    ▼
┌──────────────────┐  ┌──────────────────┐
│confirmation.accepted│ │confirmation.rejected│
└──────────┬─────────┘  └──────────┬─────────┘
           │                       │
           ▼                       ▼
┌──────────────────┐  ┌──────────────────┐
│   run.started     │  │   (operation    │
│  (proceeds with   │  │   cancelled)    │
│   confirmed       │  │                 │
│   action)         │  │                 │
└──────────────────┘  └──────────────────┘
```

**Confirmation Flow Sequence:**

1. `intent.classified` (Write category) → `gate.selected` (requiresConfirmation: true)
2. `confirmation.requested` emitted with full action details
3. **Wait for user response...**
4. On accept: `confirmation.accepted` → `run.started` (execution proceeds)
5. On reject: `confirmation.rejected` → operation cancelled
6. On timeout: `confirmation.timeout` → operation cancelled

---

## Error Events

#### `error`

Emitted when an error occurs during orchestration.

**Payload:**
```json
{
  "severity": "error",
  "code": "ORCHESTRATOR_ERROR",
  "message": "Failed to classify intent",
  "context": "Classification",
  "recoverable": false,
  "retryAction": null
}
```

| Field | Type | Description |
|-------|------|-------------|
| `severity` | string | `"error"`, `"warning"`, or `"info"` |
| `code` | string | Error code for categorization |
| `message` | string | Human-readable error message |
| `context` | string | Phase or component where error occurred |
| `recoverable` | boolean | Whether the error is recoverable |
| `retryAction` | string | Suggested retry action (if recoverable) |

---

## Event Sequences

### Chat-Only Interaction

For messages classified as `Chat` (no write operation):

```
┌─────────────────┐
│intent.classified│
│  (category: Chat)│
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  gate.selected  │
│ (targetPhase:   │
│  None/Chat)     │
└────────┬────────┘
         │
         ▼
┌─────────────────┐     ┌─────────────────┐
│ assistant.delta │ --> │ assistant.delta │ ...
│   (chunk 0)     │     │   (chunk n)     │
└────────┬────────┘     └────────┬────────┘
         │                       │
         └───────────┬───────────┘
                     │
                     ▼
            ┌─────────────────┐
            │  assistant.final│
            │   (complete msg)│
            └─────────────────┘
```

**Sequence:** `intent.classified` → `gate.selected` → `assistant.delta` (multiple) → `assistant.final`

**Note:** No `run.started` or `run.finished` events are emitted for chat-only interactions.

---

### Workflow Execution

For messages classified as `Write`, `Plan`, or `Execute`:

```
┌─────────────────┐
│intent.classified│
│ (category: Plan)│
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  gate.selected  │
│ (targetPhase:   │
│  Planner)       │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  run.started    │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  phase.started  │
│ (phase: Planner)│
└────────┬────────┘
         │
         ▼
┌─────────────────┐     ┌─────────────────┐
│    tool.call    │ --> │   tool.result   │
│ (call-uuid-001) │     │ (call-uuid-001) │
└────────┬────────┘     └────────┬────────┘
         │                       │
         ▼                       ▼
┌─────────────────┐     ┌─────────────────┐
│    tool.call    │ --> │   tool.result   │
│ (call-uuid-002) │     │ (call-uuid-002) │
└────────┬────────┘     └────────┬────────┘
         │                       │
         └───────────┬───────────┘
                     │
                     ▼
         ┌───────────────────────┐
         │    assistant.delta    │
         │    (chunk 0..n)       │
         └───────────┬───────────┘
                     │
                     ▼
         ┌───────────────────────┐
         │    assistant.final    │
         └───────────┬───────────┘
                     │
                     ▼
         ┌───────────────────────┐
         │   phase.completed     │
         └───────────┬───────────┘
                     │
                     ▼
         ┌───────────────────────┐
         │    run.finished       │
         └───────────────────────┘
```

**Sequence:** `intent.classified` → `gate.selected` → `run.started` → `phase.started` → `tool.call`/`tool.result` pairs → `assistant.delta` (multiple) → `assistant.final` → `phase.completed` → `run.finished`

---

## Error Handling Sequences

When an error occurs during orchestration:

1. An `error` event is emitted
2. If a phase was in progress, a `phase.completed` with `success: false` follows
3. If a run was in progress, a `run.finished` with `success: false` follows

**Example error sequence:**
```
┌─────────────────┐
│     error       │
│ (code: TOOL_   │
│  EXECUTION_    │
│  FAILED)        │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  phase.completed│
│   (success:     │
│    false)       │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   run.finished  │
│   (success:     │
│    false)       │
└─────────────────┘
```

---

## Legacy (v1) Compatibility

The legacy endpoint (`POST /api/chat/stream`) continues to function and transforms v2 events into legacy format:

| v2 Event Type | Legacy Type | Legacy Content |
|---------------|-------------|----------------|
| `intent.classified` | `thinking` | "Intent classified: {category} (confidence: 92%)" |
| `gate.selected` | `thinking` | "Phase selected: {phase}" |
| `assistant.delta` | `content_chunk` | Token chunk content |
| `assistant.final` | `message_complete` | Complete message content |
| `tool.call` | `thinking` | "🛠️ Calling tool: {toolName}" |
| `tool.result` | `thinking` | "✅ Tool execution complete" or "❌ Tool execution failed" |
| `phase.lifecycle` | `thinking` | Status emoji + phase status |
| `run.lifecycle` | `thinking` | Status emoji + run status |
| `error` | `error` | Error message |

**Note:** New typed events (v2) are NOT emitted to legacy clients. The adapter maps all events to the legacy `StreamingChatEvent` format.

---

## Migration Guide: v1 → v2

### For Frontend Developers

#### 1. Endpoint Change

**v1:**
```javascript
const eventSource = new EventSource('/api/chat/stream?message=...');
```

**v2:**
```javascript
const response = await fetch('/api/chat/stream-v2', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ message: '...' })
});
const reader = response.body.getReader();
```

#### 2. Event Parsing

**v1:** Events were simple strings or pre-parsed objects with `type`, `content`, `isFinal`.

**v2:** Parse the event envelope and inspect the `type` field:

```javascript
// v2 event parsing
const parseEvent = (sseData) => {
  const event = JSON.parse(sseData);
  
  switch (event.type) {
    case 'intent.classified':
      return { category: event.payload.category, confidence: event.payload.confidence };
    case 'assistant.delta':
      return { content: event.payload.content, messageId: event.payload.messageId };
    case 'assistant.final':
      return { content: event.payload.content, structuredData: event.payload.structuredData };
    // ... handle other types
  }
};
```

#### 3. Accumulating Content

**v1:**
```javascript
let content = '';
eventSource.onmessage = (e) => {
  const event = JSON.parse(e.data);
  if (event.type === 'content_chunk') {
    content += event.content;
  }
};
```

**v2:**
```javascript
let content = '';
let currentMessageId = null;

reader.read().then(function process({ done, value }) {
  if (done) return;
  
  const events = parseSSEChunk(value);
  for (const event of events) {
    if (event.type === 'assistant.delta') {
      // Validate messageId hasn't changed
      if (currentMessageId && currentMessageId !== event.payload.messageId) {
        content = ''; // Reset for new message
      }
      currentMessageId = event.payload.messageId;
      content += event.payload.content;
    }
    if (event.type === 'assistant.final') {
      // Use structuredData if available
      if (event.payload.structuredData) {
        handleStructuredResponse(event.payload.structuredData);
      }
    }
  }
});
```

#### 4. Handling Reasoning Events

**v1:** Reasoning was implicit in `thinking` events.

**v2:** Explicit `intent.classified` and `gate.selected` events provide full reasoning:

```javascript
case 'intent.classified':
  showIntentBadge(event.payload.category, event.payload.confidence);
  showReasoning(event.payload.reasoning);
  break;

case 'gate.selected':
  if (event.payload.requiresConfirmation) {
    showConfirmationDialog(event.payload.proposedAction);
  }
  break;
```

#### 5. Error Handling

**v1:** Errors were mixed into content stream.

**v2:** Explicit error events with recovery hints:

```javascript
case 'error':
  if (event.payload.recoverable) {
    showRetryButton(event.payload.retryAction);
  } else {
    showFatalError(event.payload.message);
  }
  break;
```

---

## Tool Calling Events

Events emitted during the tool calling conversation loop. These events provide real-time visibility into multi-step tool execution workflows where the LLM may request multiple tool calls across several conversation turns.

### Tool Calling Event Sequence

```
┌─────────────────────────────────────────────────────────────────┐
│                     TOOL CALLING FLOW                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────┐     ┌─────────────────┐                     │
│  │tool.call.detected│     │assistant.delta │ (optional)      │
│  │ (LLM requests  │     │ (thinking text) │                     │
│  │  tool calls)    │     └─────────────────┘                     │
│  └────────┬────────┘                                           │
│           │                                                     │
│           ▼                                                     │
│  ┌─────────────────┐     ┌─────────────────┐                     │
│  │ tool.call.started│ --> │tool.call.completed│                   │
│  │  (per tool)     │     │  or             │ (per tool)         │
│  └─────────────────┘     │tool.call.failed  │                     │
│                          └─────────────────┘                     │
│                                   │                             │
│           ┌───────────────────────┘                             │
│           │                                                     │
│           ▼                                                     │
│  ┌─────────────────┐                                             │
│  │tool.results.submitted│ (all results sent to LLM)            │
│  └────────┬────────┘                                             │
│           │                                                     │
│           ▼                                                     │
│  ┌─────────────────┐     ┌─────────────────┐                     │
│  │tool.loop.iteration│ --> │  (loop back or  │                    │
│  │   completed      │     │    complete)    │                    │
│  └─────────────────┘     └─────────────────┘                     │
│                                   │                             │
│           ┌───────────────────────┘                             │
│           ▼                                                     │
│  ┌─────────────────┐                                             │
│  │ tool.loop.completed│ (final response)                        │
│  └─────────────────┘                                             │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

### `tool.call.detected`

Emitted when the LLM requests one or more tool calls.

**Payload:**
```json
{
  "iteration": 1,
  "toolCalls": [
    {
      "toolCallId": "call_abc123",
      "toolName": "calculator",
      "argumentsJson": "{\"operation\":\"add\",\"a\":5,\"b\":3}"
    },
    {
      "toolCallId": "call_def456",
      "toolName": "file_system",
      "argumentsJson": "{\"operation\":\"list\",\"path\":\".\"}"
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `iteration` | integer | The iteration number in the loop (1-indexed) |
| `toolCalls` | array | List of tool calls requested by the LLM |
| `toolCalls[].toolCallId` | string | Unique identifier for this tool call |
| `toolCalls[].toolName` | string | Name of the tool being called |
| `toolCalls[].argumentsJson` | string | Arguments as a JSON string |

---

### `tool.call.started`

Emitted when an individual tool execution begins.

**Payload:**
```json
{
  "iteration": 1,
  "toolCallId": "call_abc123",
  "toolName": "calculator",
  "argumentsJson": "{\"operation\":\"add\",\"a\":5,\"b\":3}"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `iteration` | integer | The iteration number |
| `toolCallId` | string | Unique identifier for this tool call |
| `toolName` | string | Name of the tool being executed |
| `argumentsJson` | string | Arguments being passed to the tool |

---

### `tool.call.completed`

Emitted when a tool execution succeeds.

**Payload:**
```json
{
  "iteration": 1,
  "toolCallId": "call_abc123",
  "toolName": "calculator",
  "durationMs": 15,
  "hasResult": true,
  "resultSummary": "{\"result\":8}"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `iteration` | integer | The iteration number |
| `toolCallId` | string | Unique identifier for this tool call |
| `toolName` | string | Name of the tool that was executed |
| `durationMs` | integer | Execution duration in milliseconds |
| `hasResult` | boolean | Whether result content exists |
| `resultSummary` | string | Summary or preview of the result (truncated if large) |

---

### `tool.call.failed`

Emitted when a tool execution fails.

**Payload:**
```json
{
  "iteration": 1,
  "toolCallId": "call_abc123",
  "toolName": "calculator",
  "errorCode": "DivideByZero",
  "errorMessage": "Cannot divide by zero",
  "durationMs": 5
}
```

| Field | Type | Description |
|-------|------|-------------|
| `iteration` | integer | The iteration number |
| `toolCallId` | string | Unique identifier for this tool call |
| `toolName` | string | Name of the tool that failed |
| `errorCode` | string | Error code for categorization |
| `errorMessage` | string | Human-readable error message |
| `durationMs` | integer | Execution duration before failure |

---

### `tool.results.submitted`

Emitted when all tool results from a turn are sent back to the LLM.

**Payload:**
```json
{
  "iteration": 1,
  "resultCount": 2,
  "results": [
    {
      "toolCallId": "call_abc123",
      "toolName": "calculator",
      "isSuccess": true
    },
    {
      "toolCallId": "call_def456",
      "toolName": "file_system",
      "isSuccess": true
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `iteration` | integer | The iteration number |
| `resultCount` | integer | Number of tool results submitted |
| `results` | array | Summary of submitted results |
| `results[].toolCallId` | string | Tool call identifier |
| `results[].toolName` | string | Tool name |
| `results[].isSuccess` | boolean | Whether the execution succeeded |

---

### `tool.loop.iteration.completed`

Emitted when one full iteration (LLM call + tool executions) completes.

**Payload:**
```json
{
  "iteration": 1,
  "hasMoreToolCalls": true,
  "toolCallCount": 2,
  "durationMs": 1250
}
```

| Field | Type | Description |
|-------|------|-------------|
| `iteration` | integer | The completed iteration number |
| `hasMoreToolCalls` | boolean | Whether the LLM requested more tool calls |
| `toolCallCount` | integer | Number of tool calls executed in this iteration |
| `durationMs` | integer | Total iteration duration in milliseconds |

---

### `tool.loop.completed`

Emitted when the tool calling loop finishes normally.

**Payload:**
```json
{
  "totalIterations": 3,
  "totalToolCalls": 4,
  "completionReason": "CompletedNaturally",
  "totalDurationMs": 3500
}
```

| Field | Type | Description |
|-------|------|-------------|
| `totalIterations` | integer | Total number of LLM calls performed |
| `totalToolCalls` | integer | Total number of tool calls executed |
| `completionReason` | string | Why the loop completed (see table below) |
| `totalDurationMs` | integer | Total loop duration in milliseconds |

**Completion Reasons:**

| Reason | Description |
|--------|-------------|
| `CompletedNaturally` | Assistant produced final response without more tool calls |
| `MaxIterationsReached` | Maximum iterations limit reached |
| `Timeout` | Loop exceeded timeout limit |
| `Error` | Unrecoverable error occurred |
| `Cancelled` | Loop was cancelled via cancellation token |

---

### `tool.loop.failed`

Emitted when the tool calling loop encounters an unrecoverable error.

**Payload:**
```json
{
  "errorCode": "LlmProviderError",
  "errorMessage": "Connection timeout to LLM provider",
  "iteration": 2
}
```

| Field | Type | Description |
|-------|------|-------------|
| `errorCode` | string | Error code categorizing the failure |
| `errorMessage` | string | Human-readable error description |
| `iteration` | integer | The iteration when the error occurred |

---

## Tool Calling Event Sequences

### Single Tool Call

For a simple conversation with one tool call:

```
┌───────────────────────┐
│  tool.call.detected   │
│  (1 tool requested)   │
└───────────┬───────────┘
            │
            ▼
┌───────────────────────┐
│   tool.call.started   │
└───────────┬───────────┘
            │
            ▼
┌───────────────────────┐
│  tool.call.completed  │
└───────────┬───────────┘
            │
            ▼
┌───────────────────────┐
│ tool.results.submitted│
└───────────┬───────────┘
            │
            ▼
┌───────────────────────┐
│tool.loop.iteration    │
│     .completed        │
│  (hasMoreToolCalls:   │
│        false)         │
└───────────┬───────────┘
            │
            ▼
┌───────────────────────┐
│   tool.loop.completed │
└───────────────────────┘
```

### Multiple Parallel Tool Calls

When the LLM requests multiple tools in one turn:

```
┌───────────────────────┐
│  tool.call.detected   │
│  (3 tools requested)  │
└───────────┬───────────┘
            │
            ▼
    ┌───────┴───────┐
    │               │
    ▼               ▼
┌────────┐    ┌────────┐
│started │    │started │
│  #1    │    │  #2    │
└───┬────┘    └───┬────┘
    │               │
    ▼               ▼
┌────────┐    ┌────────┐
│completed│    │completed│
│  #1    │    │  #2    │
└───┬────┘    └───┬────┘
    │               │
    └───────┬───────┘
            │
            ▼
┌───────────────────────┐
│ tool.results.submitted│
│   (resultCount: 3)    │
└───────────┬───────────┘
            │
            ▼
┌───────────────────────┐
│tool.loop.iteration    │
│     .completed        │
└───────────────────────┘
```

### Multi-Turn Tool Calling

When the LLM requests more tools after receiving results:

```
Iteration 1                    Iteration 2
┌─────────────┐                ┌─────────────┐
│call.detected│                │call.detected│
│  (2 tools)   │                │  (1 tool)   │
└──────┬──────┘                └──────┬──────┘
       │                              │
       ▼                              ▼
  ┌────┴────┐                    ┌────┴────┐
  ▼         ▼                    ▼         │
┌────┐   ┌────┐               ┌────┐       │
│#1  │   │#2  │               │#3  │       │
└────┘   └────┘               └────┘       │
       │                              │
       ▼                              ▼
┌─────────────┐                ┌─────────────┐
│results.submitted│              │results.submitted│
└──────┬──────┘                └──────┬──────┘
       │                              │
       ▼                              ▼
┌─────────────┐                ┌─────────────┐
│loop.iteration│                │loop.iteration│
│ .completed   │                │ .completed   │
│(hasMore: true)│               │(hasMore: false)│
└─────────────┘                └──────┬──────┘
                                    │
                                    ▼
                             ┌─────────────┐
                             │loop.completed│
                             └─────────────┘
```

---

## Frontend Integration

### JavaScript Event Handler

```javascript
// Tool calling event handler for SSE streaming
class ToolCallingEventHandler {
  constructor(uiManager) {
    this.ui = uiManager;
    this.activeToolCalls = new Map();
  }

  handleEvent(event) {
    const { type, payload } = event;

    switch (type) {
      case 'tool.call.detected':
        this.onToolCallsDetected(payload);
        break;
      case 'tool.call.started':
        this.onToolCallStarted(payload);
        break;
      case 'tool.call.completed':
        this.onToolCallCompleted(payload);
        break;
      case 'tool.call.failed':
        this.onToolCallFailed(payload);
        break;
      case 'tool.results.submitted':
        this.onResultsSubmitted(payload);
        break;
      case 'tool.loop.iteration.completed':
        this.onIterationCompleted(payload);
        break;
      case 'tool.loop.completed':
        this.onLoopCompleted(payload);
        break;
    }
  }

  onToolCallsDetected(payload) {
    // Show tool call indicators in UI
    this.ui.showToolCallPanel();
    for (const tc of payload.toolCalls) {
      this.ui.addToolCallCard(tc.toolCallId, tc.toolName, tc.argumentsJson);
    }
  }

  onToolCallStarted(payload) {
    // Mark tool call as in-progress
    this.ui.updateToolCallStatus(payload.toolCallId, 'running');
  }

  onToolCallCompleted(payload) {
    // Show success with duration
    this.ui.updateToolCallStatus(payload.toolCallId, 'completed', {
      durationMs: payload.durationMs,
      resultSummary: payload.resultSummary
    });
  }

  onToolCallFailed(payload) {
    // Show error
    this.ui.updateToolCallStatus(payload.toolCallId, 'failed', {
      errorCode: payload.errorCode,
      errorMessage: payload.errorMessage
    });
  }

  onResultsSubmitted(payload) {
    // Brief notification that results were sent to LLM
    this.ui.showStatusMessage(`Sent ${payload.resultCount} tool results to assistant`);
  }

  onIterationCompleted(payload) {
    if (payload.hasMoreToolCalls) {
      this.ui.showThinkingIndicator(`Assistant is requesting more tools (iteration ${payload.iteration})...`);
    }
  }

  onLoopCompleted(payload) {
    this.ui.hideToolCallPanel();
    this.ui.showCompletionStatus({
      iterations: payload.totalIterations,
      toolCalls: payload.totalToolCalls,
      reason: payload.completionReason
    });
  }
}
```

### React Hook Example

```typescript
// useToolCallingEvents.ts
import { useState, useCallback } from 'react';

interface ToolCall {
  id: string;
  toolName: string;
  argumentsJson: string;
  status: 'pending' | 'running' | 'completed' | 'failed';
  durationMs?: number;
  error?: { code: string; message: string };
  resultSummary?: string;
}

export function useToolCallingEvents() {
  const [toolCalls, setToolCalls] = useState<ToolCall[]>([]);
  const [iteration, setIteration] = useState(0);
  const [isComplete, setIsComplete] = useState(false);

  const handleEvent = useCallback((event: any) => {
    switch (event.type) {
      case 'tool.call.detected':
        const newCalls = event.payload.toolCalls.map((tc: any) => ({
          id: tc.toolCallId,
          toolName: tc.toolName,
          argumentsJson: tc.argumentsJson,
          status: 'pending' as const
        }));
        setToolCalls(prev => [...prev, ...newCalls]);
        setIteration(event.payload.iteration);
        break;

      case 'tool.call.started':
        setToolCalls(prev =>
          prev.map(tc =>
            tc.id === event.payload.toolCallId
              ? { ...tc, status: 'running' }
              : tc
          )
        );
        break;

      case 'tool.call.completed':
        setToolCalls(prev =>
          prev.map(tc =>
            tc.id === event.payload.toolCallId
              ? {
                  ...tc,
                  status: 'completed',
                  durationMs: event.payload.durationMs,
                  resultSummary: event.payload.resultSummary
                }
              : tc
          )
        );
        break;

      case 'tool.call.failed':
        setToolCalls(prev =>
          prev.map(tc =>
            tc.id === event.payload.toolCallId
              ? {
                  ...tc,
                  status: 'failed',
                  durationMs: event.payload.durationMs,
                  error: {
                    code: event.payload.errorCode,
                    message: event.payload.errorMessage
                  }
                }
              : tc
          )
        );
        break;

      case 'tool.loop.completed':
        setIsComplete(true);
        break;
    }
  }, []);

  const reset = useCallback(() => {
    setToolCalls([]);
    setIteration(0);
    setIsComplete(false);
  }, []);

  return { toolCalls, iteration, isComplete, handleEvent, reset };
}
```

---

## Best Practices

1. **Correlation ID:** Use `correlationId` to link all events from a single user request
2. **Sequence Numbers:** Use `sequenceNumber` for ordering (timestamps may not guarantee order)
3. **Call ID Matching:** Match `tool.call` and `tool.result` events using `callId`
4. **Message ID Consistency:** All `assistant.delta` events for a single response share the same `messageId`
5. **Run Event Filtering:** Don't expect `run.started`/`run.finished` for chat-only interactions

---

## Related Documentation

- OpenSpec: `streaming-dialogue-protocol` specification
- OpenSpec: `orchestrator-event-emitter` specification
- OpenSpec: `implement-tool-calling-protocol` specification
- API Controller: `Gmsd.Web/Controllers/ChatStreamingController.cs`
- Tool Calling Adapter: `Gmsd.Web/AgentRunner/ToolCallingEventAdapter.cs`
- Gmsd.Agents README: `Gmsd.Agents/README.md`
