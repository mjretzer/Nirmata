## Context

Phase 8 turns the current `ChatPage` stub into the primary workspace chat and command surface. The backend already has command execution and orchestrator data, so the new change should classify user text into commands, dispatch them through the existing daemon command pipeline, and return a response that the frontend can render without additional inference. The frontend must keep the current UX patterns — message thread, suggestions, quick actions, timeline, and artifact chips — but source all data from the API instead of local mock state.

## Goals / Non-Goals

**Goals:**
- Provide a workspace-scoped chat endpoint that accepts freeform input and returns a structured `OrchestratorMessage`.
- Keep `ChatPage` role rendering, timeline, artifact display, suggestions, and quick actions intact while swapping in real data.
- Support either streaming or polling so the UI can show incremental progress without blocking the page.
- Keep API and frontend type shapes closely aligned to reduce adapter code.

**Non-Goals:**
- Replacing the existing command execution system.
- Designing a new conversational memory or long-term chat archive.
- Reworking the overall app layout or navigation beyond the chat flow.

## Decisions

- **Use a workspace-scoped chat endpoint in `nirmata.Api`**
  - Rationale: chat needs workspace context to produce useful orchestrator responses and command routing.
  - Alternatives considered: keep a global `/api/v1/chat` endpoint or only use `CommandsController`. Those approaches do not capture workspace state cleanly.

- **Return a structured `OrchestratorMessage` from the backend**
  - Rationale: the UI already needs role, gate, artifacts, timeline, and `nextCommand` data, so the backend should emit that shape directly.
  - Alternatives considered: return raw command output only. That would force the frontend to infer orchestration state.

- **Keep command suggestions and quick actions server-backed**
  - Rationale: the UI should present actions that match the current workspace state rather than hardcoded lists.
  - Alternatives considered: keep `mockCommandSuggestions` and `mockQuickActions` client-only. That would preserve stubs and drift from the actual command surface.

- **Prefer streaming when the backend can support it, but allow polling fallback**
  - Rationale: chat feels better with progressive updates, but command execution should still work if only a snapshot response is available.
  - Alternatives considered: require SSE only. That would add complexity and block the base implementation.

## Risks / Trade-offs

- **Command classification may be ambiguous** → Normalize obvious `aos` commands first and fail closed for unrecognized freeform requests.
- **Streaming support may take longer than the basic response path** → Make the response contract usable for both streaming and one-shot polling.
- **Chat response shape may drift from frontend types** → Add adapter tests around `OrchestratorMessage` and the chat hook mapping.
- **Workspace validation failures** → Return 404 for unknown workspace ids before any command dispatch.

## Migration Plan

1. Add the chat service and workspace-scoped controller endpoint.
2. Extend the API client and hook layer to fetch snapshot data and send chat turns.
3. Replace `ChatPage` mock state with real API-backed rendering and send flow.
4. Add tests for chat response shape, command dispatch, and role-aware UI rendering.
5. Verify an `aos status` request produces a real command response with gate and timeline context.
