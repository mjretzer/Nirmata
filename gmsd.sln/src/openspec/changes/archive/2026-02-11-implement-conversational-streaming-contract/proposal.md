# Proposal: Implement Conversational Streaming Contract

## Change ID
`implement-conversational-streaming-contract`

## Summary
Replace the current "command-in, run-summary-out" orchestrator UX with a conversational streaming contract that emits typed events over SSE. This makes reasoning gates visible and transforms the orchestrator from a silent router into an agentic conversational interface.

## Motivation
Per Remediation.md (Immediate remediation #1), the current system streams "execution status" rather than agent dialogue. Users cannot see:
- Intent classification results
- Gate selection reasoning
- Phase progress
- Tool invocations
- Streaming assistant responses

This change addresses the highest-impact fix: **"Stream gate decisions and phase reasoning as SSE events, not only run summaries."**

## Related Changes
- **Depends on:** Existing specs `streaming-dialogue-protocol` and `orchestrator-event-emitter`
- **Complements:** `redesign-ui-chat-forward` (UI rendering of these events)
- **Precedes:** Full LLM-backed chat responder implementation

## Scope

### In Scope
1. Wire up existing `streaming-dialogue-protocol` spec requirements:
   - `intent.classified` event emission
   - `gate.selected` event emission
   - `phase.started` / `phase.completed` events
   - `tool.call` / `tool.result` events
   - `assistant.delta` / `assistant.final` events
   - `run.started` / `run.finished` events (write ops only)

2. Ensure `IStreamingOrchestrator.ExecuteWithEventsAsync` produces the correct event sequence

3. Maintain backward compatibility via legacy event adapter

### Out of Scope
- New event types not in existing specs
- UI rendering changes (handled in `redesign-ui-chat-forward`)
- LLM provider implementation
- Chat responder logic (separate change)

## Success Criteria
- [ ] `POST /api/chat/stream-v2` emits typed events per `streaming-dialogue-protocol`
- [ ] Event sequence matches spec requirements for both chat and workflow paths
- [ ] Legacy endpoint continues to function via adapter
- [ ] All events validate against spec schemas

## Risks & Mitigations
| Risk | Mitigation |
|------|------------|
| Breaking existing UI | Maintain legacy endpoint; legacy adapter already exists |
| Event ordering bugs | Add integration tests with sequence validation |
| Performance overhead | Events are lightweight; streaming is already established |

## Estimation
Small (2-3 days). Most spec infrastructure exists; this is wiring and validation work.

## References
- `openspec/Remediation.md:L62-L81` - Immediate remediation item
- `openspec/specs/streaming-dialogue-protocol/spec.md` - Event definitions
- `openspec/specs/orchestrator-event-emitter/spec.md` - Emission requirements
