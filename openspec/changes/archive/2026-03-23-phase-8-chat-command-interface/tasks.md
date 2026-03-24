<<<<<<< C:/Users/James Lestler/Desktop/Projects/Nirmata/openspec/changes/phase-8-chat-command-interface/tasks.md
## 1. Backend chat service

- [x] 1.1 Add workspace-scoped chat DTOs in the API layer: request input, snapshot payload, and turn response payload aligned to `OrchestratorMessage` (`role`, `content`, `gate`, `artifacts`, `timeline`, `nextCommand`, `runId`, `logs`, `timestamp`, `agentId`).
- [x] 1.2 Implement `IChatService` (or extend `ICommandService`) to: validate workspace id; normalize direct `aos ...` input; classify freeform text into command vs conversational mode; dispatch through the existing command execution pipeline; map the result into the chat DTOs.
- [x] 1.3 Add `ChatController` endpoints: `GET /v1/workspaces/{workspaceId}/chat` for the initial snapshot and `POST /v1/workspaces/{workspaceId}/chat` for a chat turn; use streaming only if easy to support, otherwise return deterministic polling-friendly JSON.
- [x] 1.4 Fail closed on unknown workspaces with `404 Not Found`; do not dispatch commands until workspace validation passes.

## 2. Frontend chat integration

- [x] 2.1 Update `nirmata.frontend/src/app/utils/apiClient.ts` with the new chat DTOs and workspace-scoped endpoints; keep the client types aligned with the backend fields above.
- [x] 2.2 Replace `useChatMessages` mock state with API-backed snapshot loading plus chat-turn submission; if streaming is unavailable, poll or refresh after each turn instead of simulating responses locally.
- [x] 2.3 Update `ChatPage` to render real messages by role (`user`, `assistant`, `system`, `result`) and show timeline steps, artifact chips, command suggestions, quick actions, run IDs, and logs from the API response.
- [x] 2.4 Keep autocomplete and quick actions wired to the API shape: suggestions should populate the input, actions should submit the mapped command, and command-mode UX should still work for `aos` input.

## 3. Verification

- [x] 3.1 Add backend tests for: workspace 404s, command classification/normalization, and response shape parity with `OrchestratorMessage`.
- [x] 3.2 Add frontend tests for: `useChatMessages` mapping, `ChatPage` role rendering, suggestion selection, quick actions, and timeline/artifact display.
- [x] 3.3 Verify end to end that submitting `aos status` returns a real response and that gate/timeline updates appear in the thread.
=======
## 1. Backend chat service

- [x] 1.1 Add workspace-scoped chat DTOs in the API layer: request input, snapshot payload, and turn response payload aligned to `OrchestratorMessage` (`role`, `content`, `gate`, `artifacts`, `timeline`, `nextCommand`, `runId`, `logs`, `timestamp`, `agentId`).
- [x] 1.2 Implement `IChatService` (or extend `ICommandService`) to: validate workspace id; normalize direct `aos ...` input; classify freeform text into command vs conversational mode; dispatch through the existing command execution pipeline; map the result into the chat DTOs.
- [x] 1.3 Add `ChatController` endpoints: `GET /v1/workspaces/{workspaceId}/chat` for the initial snapshot and `POST /v1/workspaces/{workspaceId}/chat` for a chat turn; use streaming only if easy to support, otherwise return deterministic polling-friendly JSON.
- [x] 1.4 Fail closed on unknown workspaces with `404 Not Found`; do not dispatch commands until workspace validation passes.

## 2. Frontend chat integration

- [x] 2.1 Update `nirmata.frontend/src/app/utils/apiClient.ts` with the new chat DTOs and workspace-scoped endpoints; keep the client types aligned with the backend fields above.
- [x] 2.2 Replace `useChatMessages` mock state with API-backed snapshot loading plus chat-turn submission; if streaming is unavailable, poll or refresh after each turn instead of simulating responses locally.
- [x] 2.3 Update `ChatPage` to render real messages by role (`user`, `assistant`, `system`, `result`) and show timeline steps, artifact chips, command suggestions, quick actions, run IDs, and logs from the API response.
- [x] 2.4 Keep autocomplete and quick actions wired to the API shape: suggestions should populate the input, actions should submit the mapped command, and command-mode UX should still work for `aos` input.

## 3. Verification

- [x] 3.1 Add backend tests for: workspace 404s, command classification/normalization, and response shape parity with `OrchestratorMessage`.
- [x] 3.2 Add frontend tests for: `useChatMessages` mapping, `ChatPage` role rendering, suggestion selection, quick actions, and timeline/artifact display.
- [x] 3.3 Verify end to end that submitting `aos status` returns a real response and that gate/timeline updates appear in the thread.
>>>>>>> C:/Users/James Lestler/.windsurf/worktrees/Nirmata/Nirmata-8a8f29ae/openspec/changes/phase-8-chat-command-interface/tasks.md
