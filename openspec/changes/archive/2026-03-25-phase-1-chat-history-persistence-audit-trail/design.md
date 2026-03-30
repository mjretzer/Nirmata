## Context

Phase 1 is the backend persistence pass for chat history. The current chat surface already exists, and the frontend already calls `GET /v1/workspaces/{workspaceId}/chat` and `POST /v1/workspaces/{workspaceId}/chat`. The missing piece is durable storage and replay of the workspace thread so snapshots stay consistent across refreshes.

## Goals / Non-Goals

**Goals:**
- Persist workspace chat turns in SQLite.
- Record an append-only audit trail in `.aos/state/events.ndjson`.
- Reconstruct the current thread in timestamp order for the chat snapshot endpoint.
- Keep the public chat DTOs stable so frontend changes stay minimal.

**Non-Goals:**
- Redesigning the chat UI.
- Introducing a separate session or conversation model.
- Changing command classification, gate evaluation, or orchestrator response shaping beyond what persistence needs.

## Decisions

- **Store the thread in SQLite**
  - Rationale: the workspace thread needs relational queries, stable identifiers, and deterministic ordering.
  - Alternatives considered: keep history only in NDJSON or rebuild it from in-memory state. Those options make querying and recovery harder.

- **Keep `.aos/state/events.ndjson` as an audit log**
  - Rationale: append-only events preserve a durable trail of submitted and responded turns.
  - Alternatives considered: store history only in SQLite. That would lose the explicit workspace audit record that the roadmap calls for.

- **Keep the existing chat DTO shape stable**
  - Rationale: `ChatPage` and `useChatMessages` already depend on the current snapshot contract.
  - Alternatives considered: introduce a new contract for persisted history. That would force unnecessary frontend churn.

- **Return ordered messages from the snapshot endpoint**
  - Rationale: the UI should consume a single source of truth for the current workspace thread.
  - Alternatives considered: add a separate history endpoint first. That would duplicate the data path before the baseline persistence exists.

## Risks / Trade-offs

- **History replay can drift from the live turn flow** → Use a single persistence path for both submitted and responded events.
- **SQLite ordering must stay deterministic** → Include timestamps and stable turn identifiers in persisted rows.
- **Audit events and relational rows can get out of sync** → Write both records as part of the same turn-processing flow.
- **Workspace-not-found behavior must remain explicit** → Preserve the existing 404 path before any persistence work starts.

## Migration Plan

1. Add the chat persistence store and row model.
2. Write submitted and responded events to `.aos/state/events.ndjson` from the chat turn flow.
3. Rebuild `GET /v1/workspaces/{workspaceId}/chat` from persisted rows in oldest-first order.
4. Add tests for history ordering, matching turn IDs, and empty-workspace behavior.
5. Verify the frontend snapshot loads unchanged, but now returns durable history after refresh.
