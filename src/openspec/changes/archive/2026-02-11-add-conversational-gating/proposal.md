# Change: Implement Conversational Gating Experience

## Why

The gating engine currently routes workflow execution internally but doesn't surface its decisions as a conversational experience. Users cannot see which phase was selected, why it was selected, or confirm before execution begins. The orchestrator behaves like a command runner rather than an agentic partner. This change makes gate decisions visible and introduces a confirmation loop that transforms the orchestrator into a true conversational agent.

## What Changes

- Emit `gate.selected` events via the streaming dialogue protocol with reasoning and proposed actions
- Add confirmation gating before write-side-effect operations
- Implement structured `ProposedAction` outputs from the gating engine for validation
- Surface gate decisions in the chat UI as agent "thinking" moments
- Add `requiresConfirmation` flag to gate results based on operation destructiveness and ambiguity
- Ensure `run.started` only emits after user confirmation

## Impact

- Affected specs: `orchestrator-workflow`, `streaming-dialogue-protocol`, `chat-responder`, `intent-classification`
- Affected code: `nirmata.Agents` (gating engine, orchestrator), `nirmata.Web` (SSE streaming, UI)
- Changes the UX from silent routing to visible, explainable, confirmable agent behavior
