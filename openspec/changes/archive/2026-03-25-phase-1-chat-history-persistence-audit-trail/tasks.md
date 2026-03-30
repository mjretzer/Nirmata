## 1. Backend chat persistence

- [x] 1.1 Add a SQLite-backed chat-history model for workspace-scoped turns.
- [x] 1.2 Persist `chat.turn.submitted` and `chat.turn.responded` events to `.aos/state/events.ndjson` during turn processing.
- [x] 1.3 Update `GET /v1/workspaces/{workspaceId}/chat` to return the full ordered thread from persisted data.
- [x] 1.4 Keep `OrchestratorMessageDto` and the existing chat snapshot DTOs stable so the frontend contract does not change.

## 2. Frontend compatibility

- [x] 2.1 Keep `useChatMessages` pointed at the live snapshot endpoint.
- [x] 2.2 Render persisted turns with the existing role-aware bubble system once the backend starts returning them.
- [x] 2.3 Preserve optimistic submission only if the persisted response can still be reconciled cleanly.

## 3. Verification

- [x] 3.1 Given three persisted turns, refreshing the workspace returns those three messages in timestamp order.
- [x] 3.2 Given a completed turn, the SQLite rows and `.aos/state/events.ndjson` entries contain matching turn IDs.
- [x] 3.3 Given a workspace with no prior turns, the snapshot endpoint returns an empty ordered list instead of a placeholder thread.
