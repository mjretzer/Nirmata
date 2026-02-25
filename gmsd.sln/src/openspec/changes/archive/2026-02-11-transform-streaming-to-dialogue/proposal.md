# transform-streaming-to-dialogue — Proposal

## Summary

Transform the GMSD streaming transport from a "job runner" model that streams execution summaries into a true "agent dialogue" model that streams the orchestrator's reasoning, decisions, and conversational turns as they happen. This change makes the agent's thought process visible to users, turning the orchestrator from a silent router into a transparent, conversable agent.

## Problem Statement

### Current Pain Points

1. **Silent Orchestrator**: The orchestrator makes classification and gating decisions, but users never see them—only the final "✅ Execution Complete" summary
2. **No Conversational Visibility**: Even when the orchestrator selects phases or reasons about intent, this happens invisibly; users experience the system as a black-box job runner
3. **Missing Dialogue Events**: The SSE stream emits generic `content_chunk` events for synthesized summaries, not typed events like `intent.classified`, `gate.selected`, `assistant.delta`
4. **No Tool Call Transparency**: When tools are invoked, users don't see the call/result exchange that led to a decision
5. **Run-or-Nothing UX**: Because reasoning isn't streamed, the only visible outcome is run creation—creating the "everything becomes a run" perception

### Goals

1. **Transparent Reasoning**: Stream classification confidence, gating decisions, and phase selections as they happen
2. **Conversational Agent UX**: Users experience the orchestrator as an agent that explains its thinking, not a command executor
3. **Typed Event Protocol**: Replace generic chunks with semantically meaningful SSE events (`intent.classified`, `gate.selected`, `tool.call`, etc.)
4. **Progressive Disclosure**: Users see high-level decisions first, can expand to see tool calls and detailed reasoning
5. **Natural Turn-Taking**: Assistant messages stream token-by-token like a real conversation partner

## Related Changes

- `redesign-ui-chat-forward` — UI layout and components that will consume these events (complementary, not overlapping)
- Archive: `implement-streaming-events` (if exists from Remediation roadmap) — this is the implementation of those concepts

## Sequencing

This change builds on the existing `Gmsd.Web/ChatStreamingController` SSE infrastructure and the `Gmsd.Agents` orchestrator. It requires coordination with `redesign-ui-chat-forward` for UI rendering but is primarily a backend protocol change.

**Dependencies**:
- `redesign-ui-chat-forward` Phase 1 complete (basic streaming infrastructure exists)
- `Gmsd.Agents` orchestrator with classification and gating components

**Relationship to `redesign-ui-chat-forward`**:
- `redesign-ui-chat-forward` = UI layout and visual components
- `transform-streaming-to-dialogue` = Event protocol and behavioral streaming
- These are complementary; the UI components will render the new event types

## Capabilities

1. **streaming-dialogue-protocol** — Define the SSE event type taxonomy and JSON schema for agent dialogue events (intent classification, gating, tool calls, assistant deltas)
2. **orchestrator-event-emitter** — Modify the orchestrator to emit typed events during classification, gating, phase execution, and tool calling
3. **ui-event-renderer** — Update the web UI to render new event types with appropriate visual treatments (reasoning blocks, tool call cards, streaming message deltas)

## Out of Scope

- LLM provider implementation (assumes `ILlmProvider` exists)
- New LLM capabilities (streaming deltas assumed available)
- UI layout changes (handled by `redesign-ui-chat-forward`)
- Command grammar or natural language parsing (separate change)
- Tool implementation (assumes tool contracts exist)

## Success Criteria

- Users can see `intent.classified` event with classification result and confidence
- Users can see `gate.selected` event with target phase and reasoning
- Assistant responses stream via `assistant.delta` events token-by-token
- Tool calls display as structured cards with call parameters and results
- Phase transitions emit `phase.started` and `phase.completed` events
- Run lifecycle events (`run.started`, `run.finished`) only appear for actual write operations
- Backward compatibility: old clients can still consume generic `content_chunk` events

## Validation Strategy

- Unit tests for event serialization/deserialization
- Integration tests for orchestrator event emission
- Manual testing: classification → gating → tool call → assistant response flow visible in UI
- Event sequence validation: verify correct ordering of events
- Backward compatibility test: ensure existing clients don't break

