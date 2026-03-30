## Why

`ChatPage` already talks to the live chat API, but the current workspace thread is still transient. Phase 1 makes chat history durable so operators can refresh, revisit, and query the same thread without introducing a separate session model.

## What Changes

- Add a chat-history persistence model in SQLite for workspace-scoped turns.
- Append `chat.turn.submitted` and `chat.turn.responded` events to `.aos/state/events.ndjson` for auditability.
- Return the full ordered thread from `GET /v1/workspaces/{workspaceId}/chat` instead of a placeholder empty list.
- Keep `OrchestratorMessageDto` and the existing frontend chat contract stable so the UI does not need a rewrite.

## Impact

- `nirmata.Api` gains durable chat history storage and snapshot reconstruction for workspace threads.
- `nirmata.frontend` continues using the live snapshot endpoint, but it will now receive persisted messages after refresh.
- The chat flow becomes auditable across both relational history queries and append-only event logs.
- Verification expands to cover history ordering, matching turn IDs, and empty-thread behavior.
