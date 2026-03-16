# Migration Guide: From Command-First to Chat-First

## Overview

This guide helps existing users and developers transition from the command-first interface to the new unified chat-first approach.

## For End Users

### What's Changing

**Before (Command-First):**
```
User: /help
Assistant: [Shows command help]

User: What's the status?
Assistant: [Stub response - limited functionality]
```

**After (Chat-First):**
```
User: What's the status?
Assistant: [Rich conversational response with workspace context]

User: Can you run the tests?
Assistant: [Suggests /run test-suite command with accept/reject]
```

### Migration Steps

#### 1. Start Using Chat
- Type messages without the `/` prefix for natural conversation
- The assistant now understands context and provides detailed responses
- Ask questions, get explanations, discuss ideas

#### 2. Use Commands When Needed
- Use `/` prefix for explicit command execution
- Commands still work exactly as before
- Autocomplete helps you find the right command

#### 3. Accept Suggestions
- When the assistant suggests a command, review it
- Click Accept to execute, or Reject to continue chatting
- Suggestions are based on your message content

### Backward Compatibility

All existing commands continue to work:
- `/help` - Still shows available commands
- `/status` - Still shows project status
- `/run` - Still executes workflows
- `/plan` - Still creates plans
- `/verify` - Still verifies specifications
- `/fix` - Still attempts fixes

**No action required** - your existing command usage is fully supported.

## For Client Developers

### API Changes

#### New Endpoint: `/api/chat/stream-v2`

The new streaming endpoint provides structured events:

```http
POST /api/chat/stream-v2
Content-Type: application/x-www-form-urlencoded

command=hello&threadId=optional-correlation-id
```

**Response:** Structured streaming events (v2 format)

#### Legacy Endpoint: `/api/chat/stream` (Deprecated)

Still supported for backward compatibility:

```http
POST /api/chat/stream
Content-Type: application/x-www-form-urlencoded

command=hello&threadId=optional-correlation-id
```

**Response:** Legacy streaming events (old format)

### Event Format Changes

#### Legacy Format (Old)
```json
{
  "type": "message_start|content_chunk|message_complete|error",
  "messageId": "...",
  "content": "...",
  "timestamp": "..."
}
```

#### New Format (v2)
```json
{
  "id": "event-guid",
  "type": "intent.classified|gate.selected|assistant.delta|assistant.final|command.suggested|...",
  "timestamp": "2024-01-01T12:00:00Z",
  "correlationId": "thread-id",
  "sequenceNumber": 1,
  "payload": { ... }
}
```

### Migration Path

#### Phase 1: Parallel Support (Current)
- Both endpoints available
- Legacy endpoint returns legacy format
- New endpoint returns v2 format
- No breaking changes

#### Phase 2: Opt-In to v2 (Recommended)
```javascript
// Update your client to use v2 endpoint
const response = await fetch('/api/chat/stream-v2', {
    method: 'POST',
    body: new FormData({
        command: userInput,
        threadId: conversationId
    })
});

// Handle v2 events
for await (const event of response.body) {
    const streamEvent = JSON.parse(event);
    switch (streamEvent.type) {
        case 'intent.classified':
            // Handle intent classification
            break;
        case 'command.suggested':
            // Handle command suggestion
            break;
        case 'assistant.delta':
            // Handle streaming response
            break;
        // ... handle other event types
    }
}
```

#### Phase 3: Legacy Deprecation (Future)
- Legacy endpoint marked as deprecated
- Timeline announced for removal
- Migration tools provided

### Event Handler Updates

#### Before (Legacy)
```javascript
function handleStreamEvent(event) {
    if (event.type === 'message_start') {
        displayMessageStart(event.messageId);
    } else if (event.type === 'content_chunk') {
        appendContent(event.messageId, event.content);
    } else if (event.type === 'message_complete') {
        finalizeMessage(event.messageId);
    }
}
```

#### After (v2)
```javascript
function handleStreamEvent(event) {
    switch (event.type) {
        case 'intent.classified':
            displayIntentClassification(event.payload);
            break;
        case 'command.suggested':
            displayCommandProposal(event.payload);
            break;
        case 'assistant.delta':
            appendContent(event.payload.messageId, event.payload.content);
            break;
        case 'assistant.final':
            finalizeMessage(event.payload.messageId, event.payload.content);
            break;
        case 'error':
            displayError(event.payload);
            break;
    }
}
```

### New Event Types to Handle

#### Command Suggestion Events
```javascript
case 'command.suggested':
    const { commandName, formattedCommand, confidence, reasoning } = event.payload;
    displaySuggestion({
        command: formattedCommand,
        confidence: confidence * 100,
        reason: reasoning,
        onAccept: () => confirmSuggestion(event.payload.confirmationRequestId),
        onReject: () => rejectSuggestion(event.payload.confirmationRequestId)
    });
    break;
```

#### Intent Classification Events
```javascript
case 'intent.classified':
    const { category, confidence, reasoning } = event.payload;
    logAnalytics({
        event: 'intent_classified',
        category,
        confidence
    });
    break;
```

### Confirmation Flow

New endpoints for handling command suggestions:

```javascript
// Accept a suggestion
async function confirmSuggestion(confirmationRequestId) {
    const response = await fetch('/api/suggestion/confirm', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            confirmationRequestId,
            formattedCommand: '/run test-suite'
        })
    });
    const result = await response.json();
    // Execute the confirmed command
    executeCommand(result.command);
}

// Reject a suggestion
async function rejectSuggestion(confirmationRequestId) {
    const response = await fetch('/api/suggestion/dismiss', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            confirmationRequestId,
            reason: 'Not what I wanted',
            continueAsChat: true
        })
    });
    const result = await response.json();
    // Continue conversation if requested
}
```

### Testing Migration

#### Test v2 Endpoint
```bash
curl -X POST http://localhost:5000/api/chat/stream-v2 \
  -d "command=hello&threadId=test-123"
```

#### Verify Event Structure
```javascript
// Verify all events have required fields
function validateEvent(event) {
    return event.id && event.type && event.timestamp && event.payload;
}
```

#### Test Command Suggestions
```bash
# Send a message that should trigger a suggestion
curl -X POST http://localhost:5000/api/chat/stream-v2 \
  -d "command=can%20you%20run%20the%20tests"

# Look for 'command.suggested' event in response
```

## For DevOps/Infrastructure

### No Infrastructure Changes Required

The migration is backward compatible:
- No database schema changes
- No new services required
- No configuration changes needed
- Existing deployments continue to work

### Monitoring

Monitor these new metrics:
- Command suggestion acceptance rate
- Intent classification accuracy
- Streaming event latency
- Token budget usage

### Rollback Plan

If issues arise:
1. Clients can continue using legacy endpoint
2. Feature flags can disable suggestions
3. No data loss or corruption risk
4. Gradual rollout recommended

## Troubleshooting

### "My client stopped working"
- Check if you're using the legacy endpoint
- Legacy endpoint still works - no changes needed
- Optionally migrate to v2 endpoint for new features

### "Events look different"
- You may be using the new v2 endpoint
- Update your event handlers to match v2 format
- Or continue using legacy endpoint

### "Command suggestions not appearing"
- Verify you're using v2 endpoint
- Check that command suggestion detection is enabled
- Review LLM provider configuration

### "Streaming is slower"
- v2 endpoint provides more detailed events
- This is expected - more information = slightly more overhead
- Performance is still acceptable for real-time streaming

## Support and Questions

- Check the User Guide for usage questions
- See Developer Guide for integration questions
- Review IMPLEMENTATION_NOTES for technical details
- Contact the development team for issues

## Timeline

- **Now:** v2 endpoint available, legacy endpoint still primary
- **Q2 2024:** v2 endpoint recommended, legacy marked deprecated
- **Q4 2024:** Legacy endpoint removal announced
- **Q1 2025:** Legacy endpoint removed (estimated)

## References

- User Guide: `USER_GUIDE.md`
- Developer Guide: `DEVELOPER_GUIDE.md`
- Implementation Notes: `IMPLEMENTATION_NOTES.md`
- Streaming Protocol: `streaming-dialogue-protocol`
