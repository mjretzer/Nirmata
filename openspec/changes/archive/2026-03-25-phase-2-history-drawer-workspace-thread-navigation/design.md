## Context

Phase 1 is complete and the chat snapshot endpoint already returns the durable current workspace thread. The missing piece is a real `History` experience that makes the thread easy to inspect without fragmenting the chat model into separate sessions.

## Goals / Non-Goals

**Goals:**
- Surface the current workspace thread in a dedicated drawer.
- Show useful per-turn metadata so the drawer adds value beyond the main message list.
- Keep `ChatPage` command-first and workspace-scoped.
- Preserve the composer state and thread scroll position when the drawer opens and closes.

**Non-Goals:**
- Introduce a separate session, inbox, or conversation-list model.
- Rebuild artifact inspection or file viewing inside the history drawer.
- Change the underlying turn-processing or persistence model from Phase 1.

## Decisions

- **Use a drawer anchored to the active workspace**
  - Rationale: the roadmap says `History` should open the current workspace thread, not a different session browser.
  - Alternatives considered: route to a new page or modal. Those would be heavier and would break the page-level flow.

- **Keep the existing snapshot as the source of truth**
  - Rationale: the chat API already returns the current thread and the drawer should stay aligned with what `useChatMessages` renders.
  - Alternatives considered: add a separate history endpoint first. That would duplicate data paths before the baseline UX exists.

- **Expose only minimal thread metadata if needed**
  - Rationale: the drawer may need a count or last-updated timestamp, but repeated opens should remain cheap.
  - Alternatives considered: materialize a separate history model or duplicate full thread data into another shape. That would increase maintenance cost.

- **Keep the drawer read-only**
  - Rationale: the history view should support inspection and navigation back to the composer, not become a second editing surface.
  - Alternatives considered: allow inline editing or command execution from the drawer. That would blur the interaction model.

## Risks / Trade-offs

- **Drawer opens may become expensive** → keep the payload thin and reuse the snapshot data already loaded for the page.
- **Composer state could be disturbed** → isolate drawer state from the message input and preserve scroll position.
- **History can drift from the main thread** → source both views from the same workspace snapshot.
- **Too much metadata can clutter the UI** → show only the fields that help operators understand a prior turn.

## Migration Plan

1. Define the minimal thread metadata the drawer needs, if any.
2. Add the `History` drawer and wire it to the current workspace snapshot.
3. Render turn metadata and navigation affordances.
4. Add tests for open/close behavior, state preservation, and data consistency.
