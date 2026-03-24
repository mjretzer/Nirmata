## Why

ChatPage is still a stub, so the app cannot turn freeform user input into workspace-scoped command execution or a structured orchestrator response. That leaves chat suggestions, quick actions, timeline updates, and artifact visibility disconnected from the real AOS command flow.

## What Changes

- Add a workspace-scoped chat service/controller that accepts freeform text, classifies it into an AOS command, dispatches execution, and returns a structured `OrchestratorMessage`.
- Reuse the command execution pipeline where possible so chat and direct commands stay behaviorally aligned.
- Replace the frontend chat mock hook with API-backed snapshot and turn handling, including streaming or polling if available.
- Implement `ChatPage` as a real message thread with role-aware rendering, timeline and artifact surfaces, command suggestions, and quick actions.

## Impact

- `nirmata.Api` gains a workspace-scoped chat endpoint and response DTOs aligned with `OrchestratorMessage`.
- `nirmata.frontend` updates `useChatMessages`, `apiClient`, and `ChatPage` to consume real chat data.
- Existing command execution behavior becomes reachable through the chat interface rather than only the command runner.
- Verification coverage expands to include command dispatch, message shaping, and UI rendering.
