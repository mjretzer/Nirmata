# Architecture Decision Record: Agent Dialogue Streaming Protocol

## ADR-001: Transform Streaming to Typed Agent Dialogue Events

**Status**: Accepted  
**Date**: 2026-02-11  
**Author**: GMSD Architecture Team  
**Related**: transform-streaming-to-dialogue

---

## Context

The GMSD system uses Server-Sent Events (SSE) to stream responses from the agent orchestrator to the web UI. The current implementation follows a "job runner" pattern where:

1. The orchestrator silently makes classification and gating decisions
2. Generic `content_chunk` events stream synthesized summaries
3. Users only see "✅ Execution Complete" with final results
4. Tool invocations happen invisibly

This creates a "black box" user experience where the agent's reasoning process is hidden, leading to:
- User confusion about why certain actions were taken
- Perception that "everything becomes a run" because reasoning isn't visible
- Lack of trust in the agent's decision-making
- Inability to intervene or confirm before destructive actions

## Decision

We will transform the streaming protocol from generic chunks to **typed agent dialogue events** that expose the orchestrator's reasoning, decisions, and operations as they happen.

### Key Decisions

#### 1. Event Taxonomy with Semantic Types

Replace generic `content_chunk` with semantically meaningful event types:

| Category | Event Types | Purpose |
|----------|-------------|---------|
| **Reasoning** | `intent.classified`, `gate.selected` | Show agent's decision process |
| **Operation** | `run.lifecycle`, `phase.lifecycle`, `tool.call`, `tool.result` | Show work being performed |
| **Dialogue** | `assistant.delta`, `assistant.final` | Conversational content |
| **Error** | `error` | Error conditions |

#### 2. Common Event Envelope

All events share a common envelope structure:

```csharp
public class StreamingEvent
{
    public string Id { get; set; }                    // Event UUID
    public StreamingEventType Type { get; set; }      // Event discriminator
    public DateTimeOffset Timestamp { get; set; }     // ISO 8601
    public string? CorrelationId { get; set; }       // Conversation thread
    public long? SequenceNumber { get; set; }         // Ordering hint
    public object? Payload { get; set; }              // Type-specific data
}
```

**Rationale**: Consistent envelope enables generic processing (logging, routing) while type-specific payloads carry semantic meaning.

#### 3. Event Sink Pattern for Orchestrator Integration

Use an `IEventSink` abstraction to emit events without breaking existing orchestrator contracts:

```csharp
public interface IEventSink
{
    void Emit(StreamingEvent @event);
}

// Channel-based implementation for async streaming
public class ChannelEventSink : IEventSink
{
    private readonly Channel<StreamingEvent> _channel;
    // ...
}
```

**Rationale**: Minimizes changes to existing orchestrator logic while enabling event emission.

#### 4. Dual-Endpoint Strategy for Backward Compatibility

Maintain two endpoints:

- **v2 endpoint** (`/api/chat/stream-v2`): Emits typed `StreamingEvent` objects
- **Legacy endpoint** (`/api/chat/stream`): Maintains `StreamingChatEvent` format

**Rationale**: Allows gradual client migration without breaking existing integrations. Legacy endpoint uses adapter to transform v2 events.

#### 5. UI Renderer Registry Pattern

Implement a registry-based renderer system:

```javascript
class EventRendererRegistry {
    resolveRenderer(event) {
        return this._renderers.find(r => r.canRender(event))
            || this._defaultRenderer;
    }
}
```

**Rationale**: Decouples event types from rendering logic; enables custom renderers without modifying core code.

## Consequences

### Positive

1. **Transparent Agent UX**: Users see the agent's reasoning process, building trust
2. **Intervention Points**: `gate.selected` with `requiresConfirmation` enables user confirmation
3. **Debugging Support**: Full event log aids troubleshooting
4. **Extensibility**: New event types can be added without breaking existing renderers
5. **Analytics**: Structured events enable rich analytics on agent behavior

### Negative

1. **Increased Payload Size**: Typed events carry more metadata than simple chunks
2. **Client Complexity**: Clients must handle multiple event types instead of just appending text
3. **Event Ordering**: Network latency may cause out-of-order arrival; requires client-side sequencing
4. **Migration Effort**: Existing clients must be updated to benefit from new events

### Mitigations

| Risk | Mitigation |
|------|------------|
| Payload Size | Events are gzipped over HTTP; reasoning text is optional |
| Client Complexity | Provide default renderers; HTMX integration handles common cases |
| Event Ordering | Include `sequenceNumber`; provide `EventSequencer` utility |
| Migration Effort | Legacy endpoint remains functional; gradual adoption encouraged |

## Alternatives Considered

### Alternative 1: Extend Legacy Format with Metadata

**Approach**: Add `metadata` field to existing `StreamingChatEvent` with typed sub-events.

**Rejected**: Would couple legacy and new formats; metadata nesting adds complexity; doesn't solve the "silent orchestrator" problem at the semantic level.

### Alternative 2: WebSocket Bidirectional Protocol

**Approach**: Replace SSE with WebSockets for true bidirectional communication.

**Rejected**: WebSockets require connection management and don't work as well with HTTP proxies; confirmation flows can be handled via separate HTTP endpoints; SSE simplicity is a feature.

### Alternative 3: GraphQL Subscriptions

**Approach**: Use GraphQL subscriptions for typed event streaming.

**Rejected**: Adds significant complexity; requires GraphQL client libraries; overkill for our event taxonomy which is relatively stable.

## Implementation Notes

### Event Type Evolution

Event types use enum values (not strings) for:
- Compile-time safety in C#
- Exhaustiveness checking in TypeScript
- Clear versioning boundary

Adding new event types:
1. Add to `StreamingEventType` enum
2. Define payload class/interface
3. Create renderer (optional - falls back to default)
4. Document in Event Type Reference

### Performance Considerations

- Event emission latency target: < 50ms
- JSON serialization optimized with source generators
- Channel bounded capacity prevents memory issues
- Client-side delta coalescing reduces DOM updates

### Security Considerations

- Tool parameters may contain sensitive data; use `IncludeToolParameters` option
- Reasoning text may expose internal logic; acceptable for transparency
- Event IDs enable audit trails without exposing internal state

## Related Decisions

- [redesign-ui-chat-forward](../redesign-ui-chat-forward/proposal.md) - UI components that render these events
- [implement-streaming-events](../archive/implement-streaming-events/) - Predecessor exploration (if exists in archive)

## References

- [API Documentation](./API_DOCUMENTATION.md)
- [Event Type Reference Guide](./EVENT_TYPE_REFERENCE.md)
- [UI Renderer Development Guide](./UI_RENDERER_DEVELOPMENT_GUIDE.md)
- [Migration Guide](./MIGRATION_GUIDE.md)

## Change Log

| Date | Change | Author |
|------|--------|--------|
| 2026-02-11 | Initial ADR | Architecture Team |
