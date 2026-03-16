# transform-streaming-to-dialogue — Design Document

## Architecture Overview

This change transforms the nirmata streaming model from a **synchronous job-summary pattern** to an **asynchronous agent-dialogue pattern**. The orchestrator becomes an observable event source that emits typed events as it reasons, decides, and responds.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         Agent Dialogue Flow                                 │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  User Input ──► Classify ──► Gate ──► (Phase Selection) ──► Execute/Respond   │
│                     │         │              │                                │
│                     ▼         ▼              ▼                                │
│              ┌─────────┐  ┌─────────┐  ┌──────────┐                        │
│              │intent.  │  │ gate.   │  │ phase.   │                        │
│              │classified│  │selected │  │started   │                        │
│              └─────────┘  └─────────┘  └──────────┘                        │
│                   │            │              │                              │
│                   │            │              ▼                              │
│                   │            │        ┌──────────┐  ┌─────────┐           │
│                   │            │        │ tool.call│─►│tool.    │           │
│                   │            │        │          │  │result   │           │
│                   │            │        └──────────┘  └─────────┘           │
│                   │            │              │                              │
│                   │            │              ▼                              │
│                   │            │        ┌──────────┐                        │
│                   │            │        │assistant│                        │
│                   │            │        │.delta   │                        │
│                   │            │        └──────────┘                        │
│                   │            │              │                              │
│                   │            │              ▼                              │
│              ┌─────────┐  ┌─────────┐  ┌──────────┐                        │
│              │assistant│  │ run.    │  │ phase.   │                        │
│              │.final   │  │started  │  │completed │                        │
│              └─────────┘  └─────────┘  └──────────┘                        │
│                   │            │              │                              │
│                   ▼            ▼              ▼                              │
│              ┌──────────────────────────────────────┐                        │
│              │        message_complete             │                        │
│              └──────────────────────────────────────┘                        │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Core Design Decisions

### 1. Event Taxonomy: Three Categories of Events

Events are categorized by their purpose and rendering behavior:

| Category | Events | Purpose | UI Treatment |
|----------|--------|---------|--------------|
| **Reasoning** | `intent.classified`, `gate.selected` | Show agent's decision process | Collapsible reasoning blocks, light styling |
| **Operation** | `run.started`, `phase.started`, `phase.completed`, `tool.call`, `tool.result` | Show work being performed | Structured cards, progress indicators |
| **Dialogue** | `assistant.delta`, `assistant.final` | Conversational content | Message bubbles, streaming text |

### 2. Event Schema Design

All events share a common envelope with category-specific payloads:

```typescript
// Common envelope
type StreamingEvent = {
  id: string;           // Event UUID for deduplication/ordering
  type: string;         // Event type discriminator
  timestamp: string;    // ISO 8601
  correlationId: string; // Links to request
  payload: unknown;     // Type-specific data
}

// Reasoning events
interface IntentClassifiedPayload {
  classification: 'Chat' | 'ReadOnly' | 'Write';
  confidence: number;   // 0.0 - 1.0
  reasoning: string;  // LLM explanation of classification
}

interface GateSelectedPayload {
  targetPhase: string;
  reasoning: string;
  requiresConfirmation: boolean;
  proposedAction?: ProposedAction;
}

// Operation events
interface ToolCallPayload {
  toolName: string;
  arguments: Record<string, unknown>;
  callId: string;
}

interface ToolResultPayload {
  callId: string;
  success: boolean;
  result: unknown;
  durationMs: number;
}

interface PhaseStartedPayload {
  phase: string;
  runId?: string;
  context: Record<string, unknown>;
}

// Dialogue events
interface AssistantDeltaPayload {
  content: string;      // Token chunk
  messageId: string;    // Groups deltas into complete message
}

interface AssistantFinalPayload {
  messageId: string;
  content: string;      // Complete message for validation
  structuredData?: unknown; // Optional parsed artifacts
}
```

### 3. Orchestrator Integration Strategy

The orchestrator currently returns a single `OrchestratorResult`. We need to introduce event emission without breaking this contract:

**Approach: Event Channel Injection**

```csharp
// New interface for event-aware orchestration
public interface IStreamingOrchestrator
{
    IAsyncEnumerable<StreamingEvent> ExecuteWithEventsAsync(
        WorkflowIntent intent,
        CancellationToken ct);
}

// Implementation wraps existing orchestrator logic
public class StreamingOrchestrator : IStreamingOrchestrator
{
    private readonly IOrchestrator _innerOrchestrator;
    private readonly IEventEmitter _eventEmitter;
    
    public async IAsyncEnumerable<StreamingEvent> ExecuteWithEventsAsync(...)
    {
        // Emit classification event
        var classification = await ClassifyAsync(intent);
        yield return CreateIntentClassifiedEvent(classification);
        
        // Emit gating event
        var gateResult = await SelectGateAsync(classification);
        yield return CreateGateSelectedEvent(gateResult);
        
        // ... continue emitting through execution
    }
}
```

**Alternative: Callback/Event Sink Pattern**

For simpler integration with existing code:

```csharp
public interface IEventSink
{
    void Emit(StreamingEvent @event);
}

// Orchestrator constructor accepts optional sink
public Orchestrator(..., IEventSink? eventSink = null)
```

### 4. Backward Compatibility Strategy

Old clients expect `StreamingChatEvent` with types: `message_start`, `thinking`, `content_chunk`, `message_complete`.

**Dual-Mode Controller**:
- New endpoint: `POST /api/chat/stream-v2` — emits typed events
- Legacy endpoint: `POST /api/chat/stream` — maintains old behavior
- Or: Header-based negotiation `Accept: application/vnd.nirmata.streaming.v2+json`

**Legacy Adapter**: Transform new events into old format for existing clients:
- `intent.classified` + `gate.selected` → `thinking` event
- `assistant.delta` → `content_chunk` events
- `assistant.final` → final `content_chunk` with `IsFinal=true`
- `message_complete` unchanged

### 5. UI Rendering Strategy

The UI receives events and renders them with appropriate visual treatments:

```
┌─────────────────────────────────────────────────────────────┐
│  User: "plan the foundation phase"                          │
├─────────────────────────────────────────────────────────────┤
│  🤔 Thinking...                                              │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ Intent classified as: Write (confidence: 0.92)      │   │
│  │ Reasoning: User wants to create a plan, which       │   │
│  │ requires write access to workspace...               │   │
│  └─────────────────────────────────────────────────────┘   │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ Gate selected: Planner                              │   │
│  │ Reasoning: Input matches planning intent pattern    │   │
│  │ Action: Create phase plan for foundation            │   │
│  │ [Confirm] [Cancel]                                  │   │
│  └─────────────────────────────────────────────────────┘   │
├─────────────────────────────────────────────────────────────┤
│  ▶ Phase started: Planner                                   │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ 🔧 Tool: read_workspace_specs                       │   │
│  │ Args: { "scope": "foundation" }                       │   │
│  │ ✅ Result: 3 specs found                            │   │
│  └─────────────────────────────────────────────────────┘   │
├─────────────────────────────────────────────────────────────┤
│  🤖 I've analyzed the foundation phase requirements.       │
│     Based on the specs, I'll create a plan with 5 tasks... │
├─────────────────────────────────────────────────────────────┤
│  ✓ Phase completed: Planner                                 │
│  ✓ Run finished: RUN-2024-001                               │
└─────────────────────────────────────────────────────────────┘
```

## Integration Points

### With `redesign-ui-chat-forward`

The UI components from `redesign-ui-chat-forward` will be extended with new renderers:

| Component | Current | Extension |
|-----------|---------|-----------|
| `ChatThread` | Renders `content_chunk` as text | Add renderers for `tool.call`, `gate.selected` |
| `StreamingResponse` | Simple text streaming | Multi-event streaming with state machine |
| `DetailPanel` | Static content | Auto-populate from `tool.result` events |

### With `nirmata.Agents` Orchestrator

The orchestrator planes emit events at specific points:

| Plane | Event Emission Point |
|-------|---------------------|
| Control Plane | After classification, after gate selection |
| Planning Plane | Phase start/end, tool calls for context gathering |
| Execution Plane | Phase start/end, all tool calls, subagent deltas |
| Verification Plane | Phase start/end, verification tool calls |

### With LLM Provider

The `ILlmProvider` interface needs to support streaming:

```csharp
public interface ILlmProvider
{
    // Existing
    Task<LlmResponse> CompleteAsync(LlmRequest request);
    
    // New: Streaming support for dialogue
    IAsyncEnumerable<LlmDelta> StreamCompletionAsync(
        LlmRequest request,
        CancellationToken ct);
}
```

## Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| Event ordering issues | Include sequence numbers; client-side reordering buffer |
| Event loss on disconnect | Idempotent events with replay capability |
| UI complexity | Incremental rollout: reasoning blocks first, then tool cards |
| Performance overhead | Event emission is async fire-and-forget; no blocking |
| LLM streaming latency | Configurable chunking; coalesce rapid small deltas |

## Success Metrics

- **Event Coverage**: 100% of classification/gating decisions emit events
- **Latency**: Events emitted within 50ms of decision point
- **UI Rendering**: All event types have dedicated visual treatments
- **Backward Compatibility**: Legacy clients continue to function
- **User Perception**: User interviews report "feels like talking to an agent"
