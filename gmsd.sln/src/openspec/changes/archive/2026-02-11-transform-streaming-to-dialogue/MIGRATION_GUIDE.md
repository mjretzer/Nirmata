# Streaming API Migration Guide

## Overview

This guide helps you migrate from the legacy streaming API (v1) to the new dialogue streaming API (v2).

## What's New in v2

The v2 streaming API introduces:

- **Typed Events**: Instead of generic `content_chunk` events, you now receive semantically meaningful events like:
  - `intent.classified` - Intent classification with confidence scores
  - `gate.selected` - Gating decision with reasoning
  - `tool.call` / `tool.result` - Tool invocation transparency
  - `assistant.delta` / `assistant.final` - Streaming assistant responses
  - `phase.lifecycle` - Phase execution tracking
  - `run.lifecycle` - Run execution tracking

- **Transparent Reasoning**: See the orchestrator's decision-making process in real-time

- **Better Tool Visibility**: Tool calls and results are now visible events

## API Endpoints

### Legacy Endpoint (v1)
```
POST /api/chat/stream
Content-Type: application/x-www-form-urlencoded
```

### New Endpoint (v2)
```
POST /api/chat/stream-v2
Content-Type: application/x-www-form-urlencoded
```

## Backward Compatibility

The legacy endpoint `/api/chat/stream` continues to work and now uses the new streaming orchestrator internally while maintaining the old response format.

### Accept Header Negotiation

You can request legacy format from the v2 endpoint using the Accept header:

```
POST /api/chat/stream-v2
Accept: application/vnd.gmsd.legacy+json
```

## Event Format Comparison

### Legacy Format (v1)
```json
{
  "type": "content_chunk",
  "messageId": "abc123",
  "content": "Processing your request...",
  "isFinal": false,
  "timestamp": "2024-01-15T10:30:00Z",
  "metadata": {}
}
```

### New Format (v2) - Intent Classified
```json
{
  "id": "def456",
  "type": "IntentClassified",
  "timestamp": "2024-01-15T10:30:00Z",
  "correlationId": "thread-123",
  "sequenceNumber": 1,
  "payload": {
    "category": "Write",
    "confidence": 0.95,
    "reasoning": "The user is requesting a code change...",
    "userInput": "plan the architecture"
  }
}
```

### New Format (v2) - Assistant Delta
```json
{
  "id": "ghi789",
  "type": "AssistantDelta",
  "timestamp": "2024-01-15T10:30:01Z",
  "correlationId": "thread-123",
  "sequenceNumber": 5,
  "payload": {
    "messageId": "msg-456",
    "content": "I'll help you",
    "index": 0
  }
}
```

## JavaScript Migration

### Before (Legacy)
```javascript
const eventSource = new EventSource('/api/chat/stream');
eventSource.onmessage = (event) => {
  const data = JSON.parse(event.data);
  if (data.type === 'content_chunk') {
    appendText(data.content);
  }
};
```

### After (v2 with HTMX Integration)
```html
<div hx-ext="sse-v2" 
     sse-connect="/api/chat/stream-v2" 
     sse-swap="#chat-container">
</div>
```

### After (v2 with Custom JavaScript)
```javascript
import { HtmxSseIntegration } from './js/htmx-sse-integration.js';

const integration = new HtmxSseIntegration({
  endpoint: '/api/chat/stream-v2',
  threadId: 'my-thread',
  chatOnly: false
});

// Enable event sequencing for out-of-order handling
integration.enableSequencing({
  bufferSize: 50,
  maxWaitMs: 200
});

// Listen for events
document.addEventListener('htmx-sse:event:rendered', (e) => {
  console.log('Event rendered:', e.detail);
});

// Connect
await integration.connect();
```

## Event Types Reference

| Event Type | Description | Payload Type |
|------------|-------------|--------------|
| `IntentClassified` | Intent classification result | `IntentClassifiedPayload` |
| `GateSelected` | Gating phase selection | `GateSelectedPayload` |
| `ToolCall` | Tool invocation | `ToolCallPayload` |
| `ToolResult` | Tool execution result | `ToolResultPayload` |
| `PhaseLifecycle` | Phase start/completion | `PhaseLifecyclePayload` |
| `AssistantDelta` | Assistant message chunk | `AssistantDeltaPayload` |
| `AssistantFinal` | Assistant message complete | `AssistantFinalPayload` |
| `RunLifecycle` | Run start/finish | `RunLifecyclePayload` |
| `Error` | Error event | `ErrorPayload` |

## Configuration Options

### StreamingOrchestrationOptions

```javascript
const options = {
  // Event emission controls
  EmitIntentClassified: true,   // Emit intent classification events
  EmitGateSelected: true,       // Emit gating decision events
  EmitToolCalls: true,          // Emit tool call events
  EmitPhaseLifecycle: true,     // Emit phase lifecycle events
  EmitRunLifecycle: true,       // Emit run lifecycle events
  EmitAssistantDeltas: true,    // Emit assistant delta events
  
  // Behavior controls
  IncludeToolParameters: false, // Include raw tool parameters (security)
  IncludeFullReasoning: true,   // Include full reasoning text
  EnableSequenceNumbers: true,   // Include sequence numbers
  
  // Chat-only mode (suppresses run/phase events)
  chatOnly: false
};
```

## Client-Side Event Sequencing

To handle out-of-order events:

```javascript
import { EventSequencer } from './js/event-sequencer.js';

const sequencer = new EventSequencer({
  bufferSize: 100,      // Max events to buffer
  maxWaitMs: 500,       // Max wait for missing events
  validateSequence: true, // Validate sequence numbers
  useTimestamps: true    // Use timestamps for sorting
});

sequencer.onRelease((orderedEvent) => {
  renderEvent(orderedEvent);
});

sequencer.onGap((gap) => {
  console.warn(`Gap detected: expected ${gap.expected}, got ${gap.actual}`);
});
```

## Migration Checklist

- [ ] Update endpoint URL from `/api/chat/stream` to `/api/chat/stream-v2`
- [ ] Handle new event types (not just `content_chunk`)
- [ ] Update event parsing to use `event.type` enum values
- [ ] Add support for typed payloads via `event.payload`
- [ ] Consider enabling event sequencing for robustness
- [ ] Update UI to render new event types appropriately
- [ ] Test with both streaming and non-streaming scenarios

## Support

For questions or issues during migration:
1. Check the event type reference above
2. Review the JavaScript integration examples
3. Enable debug logging: `_logger.LogLevel = Debug`

## Deprecation Timeline

- **Current**: v1 and v2 both supported
- **v1 endpoint**: No deprecation planned - continues to work via adapter
- **Recommendation**: New implementations should use v2 for richer event data
