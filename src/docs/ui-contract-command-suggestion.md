# UI Contract: Command Suggestion Mode

## Overview

This document describes the event schema and UI flow for the command suggestion mode feature. When a user enters freeform text that the system recognizes as potentially being a command, the backend emits a `command.suggested` event that the frontend can use to display a suggestion card.

## Event Schema

### command.suggested

Emitted when the LLM-based command suggester detects that natural language input could be mapped to an explicit command.

```json
{
  "id": "evt_abc123",
  "type": "command.suggested",
  "timestamp": "2026-02-11T12:00:00Z",
  "correlationId": "thread-xyz789",
  "sequenceNumber": 3,
  "payload": {
    "commandName": "plan",
    "arguments": ["--phase-id", "PH-0001"],
    "formattedCommand": "/plan --phase-id PH-0001",
    "confidence": 0.85,
    "reasoning": "User wants to create a plan for the foundation phase",
    "originalInput": "plan the foundation phase",
    "confirmationRequestId": "conf_req_123"
  }
}
```

**Payload Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `commandName` | string | The suggested command (e.g., "run", "plan", "status") |
| `arguments` | string[] | Arguments for the command |
| `formattedCommand` | string | Fully formatted command string ready to execute |
| `confidence` | number | 0.0-1.0 score indicating suggestion certainty |
| `reasoning` | string | Explanation for why this command was suggested |
| `originalInput` | string | The user's original natural language input |
| `confirmationRequestId` | string | Unique ID for tracking the confirmation flow |

## UI Flow

### 1. Display Suggestion Card

When the frontend receives a `command.suggested` event, it should display a suggestion card:

```
┌─────────────────────────────────────────────────────────────┐
│  💡 Did you mean to run this command?                       │
│                                                             │
│  /plan --phase-id PH-0001                                   │
│                                                             │
│  "User wants to create a plan for the foundation phase"     │
│                                                             │
│  [Execute]  [Dismiss]                                       │
└─────────────────────────────────────────────────────────────┘
```

**Card Elements:**
- **Header**: Indicates this is a command suggestion
- **Command Preview**: The formatted command that would be executed
- **Reasoning**: Why the system thinks this is what the user wants
- **Execute Button**: Confirms and executes the suggested command
- **Dismiss Button**: Rejects the suggestion and continues as chat

### 2. User Action Handling

#### Execute Flow

When the user clicks "Execute":

1. **Frontend**: POST to `/api/suggestion/confirm`
   ```json
   {
     "confirmationRequestId": "conf_req_123",
     "formattedCommand": "/plan --phase-id PH-0001",
     "originalInput": "plan the foundation phase"
   }
   ```

2. **Backend Response**:
   ```json
   {
     "confirmed": true,
     "confirmationRequestId": "conf_req_123",
     "command": "/plan --phase-id PH-0001",
     "eventId": "evt_def456",
     "message": "Suggestion confirmed. Execute the returned command to proceed."
   }
   ```

3. **Event Emitted**: `suggested.command.confirmed`
   ```json
   {
     "id": "evt_def456",
     "type": "suggested.command.confirmed",
     "payload": {
       "confirmationRequestId": "conf_req_123",
       "commandName": "plan",
       "formattedCommand": "/plan --phase-id PH-0001",
       "originalInput": "plan the foundation phase"
     }
   }
   ```

4. **Frontend Action**: Execute the command via the standard streaming endpoint

#### Dismiss Flow

When the user clicks "Dismiss":

1. **Frontend**: POST to `/api/suggestion/dismiss`
   ```json
   {
     "confirmationRequestId": "conf_req_123",
     "reason": "I just want to chat",
     "continueAsChat": true
   }
   ```

2. **Backend Response**:
   ```json
   {
     "dismissed": true,
     "confirmationRequestId": "conf_req_123",
     "continueAsChat": true,
     "eventId": "evt_ghi789",
     "message": "Suggestion dismissed. Continuing as chat."
   }
   ```

3. **Event Emitted**: `suggested.command.rejected`
   ```json
   {
     "id": "evt_ghi789",
     "type": "suggested.command.rejected",
     "payload": {
       "confirmationRequestId": "conf_req_123",
       "commandName": "plan",
       "rejectionReason": "I just want to chat",
       "continueAsChat": true
     }
   }
   ```

4. **Frontend Action**: Continue conversation as normal chat (or just close the suggestion card)

## Example Complete Flow

**User Input**: "plan the foundation phase"

**Event Sequence**:

1. `intent.classified` - System classifies input as chat (freeform)
2. `command.suggested` - Suggestion mode triggers and proposes `/plan --phase-id PH-0001`
3. User clicks "Execute"
4. `suggested.command.confirmed` - Confirmation recorded
5. `run.started` - Command execution begins
6. `phase.started` - Planner phase begins
7. `assistant.delta` - Streaming response chunks
8. `assistant.final` - Complete response
9. `phase.completed` - Planner phase ends
10. `run.finished` - Command execution complete

## Error Handling

### No Suggestion Scenario

If the input doesn't match any command pattern, no `command.suggested` event is emitted. The flow proceeds as normal chat:

1. `intent.classified` (category: "Chat")
2. `assistant.delta` (streaming response)
3. `assistant.final`

### LLM Unavailable

If the LLM service is unavailable, the system gracefully falls back to chat mode without emitting a suggestion event.

### Low Confidence

Suggestions with confidence below 0.7 (configurable) are not emitted. The input is treated as chat.

## Configuration

The suggestion mode behavior can be configured via `appsettings.json`:

```json
{
  "nirmataAgents": {
    "CommandSuggestion": {
      "EnableSuggestionMode": true,
      "ConfidenceThreshold": 0.7,
      "MaxInputLength": 1000,
      "IncludeExamples": true
    }
  }
}
```

| Setting | Description | Default |
|---------|-------------|---------|
| `EnableSuggestionMode` | Master switch for the feature | `true` |
| `ConfidenceThreshold` | Minimum confidence to suggest | `0.7` |
| `MaxInputLength` | Max input length to analyze | `1000` |
| `IncludeExamples` | Include examples in reasoning | `true` |

## Event Type Reference

| Event Type | Direction | Description |
|------------|-----------|-------------|
| `command.suggested` | Server → Client | Backend proposes a command |
| `suggested.command.confirmed` | Server → Client | User confirmed the suggestion |
| `suggested.command.rejected` | Server → Client | User rejected the suggestion |

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/suggestion/confirm` | POST | Confirm and execute suggested command |
| `/api/suggestion/dismiss` | POST | Dismiss suggestion and optionally continue as chat |
| `/api/chat/stream-v2` | POST | Streaming endpoint that emits suggestion events |

## JavaScript/TypeScript Types

```typescript
interface CommandSuggestedPayload {
  commandName: string;
  arguments?: string[];
  formattedCommand?: string;
  confidence: number;
  reasoning?: string;
  originalInput?: string;
  confirmationRequestId?: string;
}

interface SuggestedCommandConfirmedPayload {
  confirmationRequestId: string;
  commandName: string;
  formattedCommand?: string;
  originalInput?: string;
}

interface SuggestedCommandRejectedPayload {
  confirmationRequestId: string;
  commandName: string;
  formattedCommand?: string;
  rejectionReason?: string;
  continueAsChat: boolean;
}

type StreamingEventType = 
  | 'command.suggested'
  | 'suggested.command.confirmed'
  | 'suggested.command.rejected'
  | 'intent.classified'
  | 'gate.selected'
  | 'assistant.delta'
  | 'assistant.final'
  | // ... other event types

interface StreamingEvent<T = unknown> {
  id: string;
  type: StreamingEventType;
  timestamp: string;
  correlationId?: string;
  sequenceNumber?: number;
  payload: T;
}
```
