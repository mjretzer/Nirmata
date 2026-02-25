# API Documentation: v2 Streaming Endpoint

## Overview

The `/api/chat/stream-v2` endpoint provides a typed, event-driven streaming protocol for agent dialogue. Unlike the legacy streaming endpoint that emits generic `content_chunk` events, the v2 endpoint emits semantically meaningful events that represent the agent's reasoning, decisions, and conversational turns.

## Endpoint Details

### Base URL

```
POST /api/chat/stream-v2
```

### Request

**Content-Type:** `application/x-www-form-urlencoded`

**Parameters:**

| Parameter   | Type    | Required | Description                                       |
|-------------|---------|----------|---------------------------------------------------|
| `command`   | string  | Yes      | The user input/command to process                 |
| `threadId`  | string  | No       | Conversation thread identifier (generates new if omitted) |
| `chatOnly`  | boolean | No       | If true, suppresses run-related events            |

**Example Request:**

```bash
curl -X POST http://localhost:5000/api/chat/stream-v2 \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -H "Accept: application/json" \
  -d "command=plan the foundation phase" \
  -d "chatOnly=false"
```

### Response Format

**Content-Type:** `application/json` (Server-Sent Events)

The response is an `IAsyncEnumerable<StreamingEvent>` streamed as newline-delimited JSON (NDJSON).

Each line is a JSON object representing a single event:

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "type": "IntentClassified",
  "timestamp": "2026-02-11T14:30:00Z",
  "correlationId": "thread-123",
  "sequenceNumber": 1,
  "payload": { /* event-specific payload */ }
}
```

### Accept Header Negotiation

| Accept Header | Response Format | Description |
|---------------|-----------------|-------------|
| `application/json` (default) | `StreamingEvent` | Typed event protocol (v2) |
| `application/vnd.gmsd.legacy+json` | `StreamingChatEvent` | Legacy format compatibility |

## Event Types

The v2 endpoint emits the following event types:

### Reasoning Events

| Event Type | Description | Payload |
|------------|-------------|---------|
| `IntentClassified` | Intent classification result | `{ classification, confidence, reasoning }` |
| `GateSelected` | Gating decision with target phase | `{ targetPhase, reasoning, requiresConfirmation }` |

### Operation Events

| Event Type | Description | Payload |
|------------|-------------|---------|
| `RunLifecycle` | Run started/finished | `{ status, runId, duration }` |
| `PhaseLifecycle` | Phase started/completed | `{ status, phase, runId, context }` |
| `ToolCall` | Tool invocation | `{ toolName, arguments, callId }` |
| `ToolResult` | Tool execution result | `{ callId, success, result, durationMs }` |

### Dialogue Events

| Event Type | Description | Payload |
|------------|-------------|---------|
| `AssistantDelta` | Token-by-token streaming | `{ content, messageId }` |
| `AssistantFinal` | Complete assistant message | `{ messageId, content, structuredData }` |

### Error Events

| Event Type | Description | Payload |
|------------|-------------|---------|
| `Error` | Error condition | `{ code, message, severity, recoverable }` |

## Event Flow Example

For a write workflow with the command "plan the foundation phase":

```
1. IntentClassified
   {"classification":"Write","confidence":0.92,"reasoning":"User wants to create..."}

2. GateSelected
   {"targetPhase":"Planner","reasoning":"Input matches planning intent...","requiresConfirmation":false}

3. RunLifecycle (started)
   {"status":"started","runId":"RUN-2024-001"}

4. PhaseLifecycle (started)
   {"status":"started","phase":"Planner","runId":"RUN-2024-001"}

5. ToolCall
   {"toolName":"read_workspace_specs","arguments":{"scope":"foundation"},"callId":"call-001"}

6. ToolResult
   {"callId":"call-001","success":true,"result":{"specs":3},"durationMs":145}

7. AssistantDelta (multiple events)
   {"content":"I've ","messageId":"msg-001"}
   {"content":"analyzed ","messageId":"msg-001"}
   {"content":"the ","messageId":"msg-001"}
   ...

8. AssistantFinal
   {"messageId":"msg-001","content":"I've analyzed...","structuredData":null}

9. PhaseLifecycle (completed)
   {"status":"completed","phase":"Planner","runId":"RUN-2024-001"}

10. RunLifecycle (finished)
    {"status":"finished","runId":"RUN-2024-001","duration":"PT2.5S"}
```

## Chat-Only Mode

When `chatOnly=true`, run lifecycle events are suppressed:

```bash
curl -X POST /api/chat/stream-v2 \
  -d "command=hello" \
  -d "chatOnly=true"
```

**Event sequence for chat-only:**
```
1. IntentClassified (Chat)
2. AssistantDelta (streaming tokens)
3. AssistantFinal
```

No `RunLifecycle` or `PhaseLifecycle` events are emitted.

## Error Handling

### Client-Side Errors

When the command is empty:

```json
{
  "type": "Error",
  "payload": {
    "code": "EMPTY_COMMAND",
    "message": "Command cannot be empty",
    "severity": "error",
    "recoverable": true
  }
}
```

### Server-Side Errors

Errors during execution emit `Error` events with context:

```json
{
  "type": "Error",
  "payload": {
    "code": "CLASSIFICATION_FAILED",
    "message": "Failed to classify intent",
    "phase": "Classification",
    "severity": "error",
    "recoverable": true,
    "retryAction": "resubmit"
  }
}
```

### Connection Errors

- **Network failure:** Stream terminates; client should reconnect with same `threadId` for continuity
- **Cancellation:** Use `POST /api/chat/cancel/{streamId}` to abort

## HTMX Integration

The v2 endpoint is designed to work with HTMX SSE extension:

```html
<div hx-ext="sse" sse-connect="/api/chat/stream-v2" sse-swap="message">
  <!-- Events will be swapped here -->
</div>
```

See the [UI Renderer Development Guide](./UI_RENDERER_DEVELOPMENT_GUIDE.md) for client-side rendering details.

## Backward Compatibility

The legacy endpoint (`/api/chat/stream`) continues to work and internally uses the v2 streaming orchestrator with automatic event transformation:

```csharp
// Legacy endpoint transforms v2 events to legacy format
assistant.delta → content_chunk
assistant.final → content_chunk (isFinal=true)
intent.classified + gate.selected → thinking
```

## Performance Considerations

- **Event Latency:** < 50ms from decision point to event emission
- **Chunk Size:** Assistant deltas are coalesced if multiple arrive within 16ms
- **Buffering:** Server buffers up to 1000 events; client should implement backpressure

## See Also

- [Event Type Reference Guide](./EVENT_TYPE_REFERENCE.md)
- [Migration Guide from Legacy Streaming](./MIGRATION_GUIDE.md)
- [Architecture Decision Record](./ADR_STREAMING_PROTOCOL.md)
