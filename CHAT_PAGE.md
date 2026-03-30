# Chat Page Roadmap

## Business Logic & OpenSpec Proposal Catalog
This roadmap tracks the next layer of `ChatPage` work. The current Phase 8 chat surface is already wired end to end, so the remaining work is about deepening the business logic, persistence model, and operator UX.

Routing principle:
- Each phase below will be implemented as its own OpenSpec proposal.
- The scope of each proposal should stay narrow and testable.
- The chat surface remains workspace-scoped and command-first.

### A. Chat surface decisions already locked
| Area | Decision |
| :--- | :--- |
| History storage | Persist in both `.aos/state/events.ndjson` and SQLite, with append-only events for audit and relational rows for thread queries |
| History UX | `History` opens a drawer for the current workspace thread, not a separate sessions list |
| Artifact UX | Artifact chips open a detail panel first, then route to the correct native page/view |
| Interaction model | Chat stays command-first; conversational input is only a classifier/routing layer |

### B. Current implementation status
- `nirmata.Api` already exposes `GET /v1/workspaces/{workspaceId}/chat` and `POST /v1/workspaces/{workspaceId}/chat`.
- `useChatMessages` already calls the live chat API via `domainClient.getChatSnapshot()` and `domainClient.postChatTurn()`; it is not on mock data.
- `IChatService` already normalizes `aos ...` input, classifies freeform text, evaluates gate state, and returns `OrchestratorMessageDto` data.
- `useChatMessages` already loads snapshots, submits turns, and refreshes suggestions and quick actions.
- `ChatPage` already renders role-aware messages, timeline steps, artifact chips, suggestions, quick actions, and command-mode UX.
- `ROADMAP.md` Phase 8 is complete, so this document focuses on the next business-logic expansion rather than the initial chat baseline.

### C. Agent framework audit
- The core `Orchestrator` classify → gate → dispatch → validate loop is real, not a stub.
- The documented 8-step `GatingEngine` priority chain is also real and matches the control-plane flow.
- `SubagentOrchestrator` is fully implemented, but it is not layered under `TaskExecutor`; both paths call the tool-calling loop independently.
- `FixPlannerHandler` exists as a real handler, but the main control-plane path still needs the missing wiring that routes fix-loop work into it.
- The confirmation flow still has a gap: destructive runs can reach a simulated rejection path instead of a live pause-and-resume confirmation flow.
- The control plane includes real production classes that are not described as separate agents here: `InputClassifier`, `ChatResponder`, and `ReadOnlyHandler`.

### D. What still needs to be built
- Durable chat history that can be reloaded and queried per workspace; the client wiring already exists, so Phase 1 is backend persistence only.
- A real `History` drawer with timestamps, gate state, and turn context.
- A thin artifact detail panel with a clear route-to-source action.
- Explicit loading, empty, and error states in the page and hook layer, especially for the silent snapshot-load failure path.
- Stronger command classification and response-shape verification.
- Better test coverage for the full chat flow.
- Clarify what `auto` mode should do relative to manual `chat` and `command` modes.
- Review `submitTurn` for stale-closure risk around `isSubmitting` before adding persistence-heavy flows.

## Phase 1: Chat history persistence and audit trail
**Goal:** make the current workspace thread durable and queryable without inventing a new session model.

**OpenSpec proposal:** one dedicated proposal for chat thread persistence. This phase is backend-first; the frontend already talks to the live chat API and only needs the snapshot data populated.

### Backend
- [x] Add a chat-history persistence model that stores turns in SQLite.
- [x] Append `chat.turn.submitted` and `chat.turn.responded` events to `.aos/state/events.ndjson`.
- [x] Return the full ordered thread from `GET /v1/workspaces/{workspaceId}/chat`.
- [x] Keep `OrchestratorMessageDto` stable so the frontend contract does not churn.

### Frontend
- [x] Keep `useChatMessages` pointed at the live snapshot endpoint; no mock-data re-wiring is needed.
- [x] Render persisted turns with the same role-aware bubble system once the backend starts returning them.
- [x] Preserve optimistic submission only if the persisted result can still be reconciled cleanly.

### Verification
- [x] Given three persisted turns, refreshing the workspace returns those three messages in timestamp order.
- [x] Given a completed turn, the SQLite rows and `.aos/state/events.ndjson` entries contain matching turn IDs.
- [x] Given a workspace with no prior turns, the snapshot endpoint returns an empty ordered list instead of a placeholder thread.

## Phase 2: History drawer and workspace thread navigation
**Goal:** make `History` useful by surfacing the current workspace thread in a dedicated drawer once Phase 1 persistence exists.

**OpenSpec proposal:** one dedicated proposal for chat-thread UX.

### Backend
- [ ] Expose any thread metadata needed by the drawer, such as turn count or last updated timestamp.
- [ ] Keep history retrieval cheap enough for repeated drawer opens.

### Frontend
- [ ] Replace the placeholder `History` button action with a slide-over drawer.
- [ ] Show the current workspace thread in reverse chronological and chronological forms if useful.
- [ ] Display per-turn metadata such as timestamp, role, gate state, run id, and next command.
- [ ] Add a quick way to jump back to the active message composer from the drawer.

### Verification
- [ ] Given a workspace with a persisted thread, clicking `History` opens a drawer for that same workspace thread.
- [ ] Given the drawer is open, it renders the same source-of-truth turns and metadata as the main thread.
- [ ] Given the drawer is closed, the composer state and scroll position remain unchanged.

## Phase 3: Artifact detail panel and native navigation
**Goal:** make artifacts actionable without teaching `ChatPage` how to render every artifact type.

**OpenSpec proposal:** one dedicated proposal for artifact inspection and navigation.

### Backend
- [ ] Make artifact metadata available in a consistent shape for the panel.
- [ ] Include artifact type, path, run/task linkage, and preview data where available.
- [ ] Keep artifact references thin so the page can route to native views.

### Frontend
- [ ] Replace direct artifact click stubs with a detail panel.
- [ ] Show artifact type, path, associated run or task, and a short preview.
- [ ] Add a `View` action that routes to the correct page, drawer, or file view.
- [ ] Keep the panel small and avoid building a raw-file viewer into `ChatPage`.

### Verification
- [ ] Clicking an artifact opens the detail panel first.
- [ ] The panel routes to the correct native view when the user chooses `View`.
- [ ] `ChatPage` stays focused on chat and command execution, not file rendering.

## Phase 4: Command classifier and execution guardrails
**Goal:** keep chat command-first while making classification, normalization, and routing stricter.

**OpenSpec proposal:** one dedicated proposal for command-routing rules.

### Backend
- [ ] Preserve the existing `aos ...` normalization path.
- [ ] Keep freeform text as a routing input only; do not let it imply unexecuted work.
- [ ] Return explicit status/help responses when input is ambiguous.
- [ ] Keep unknown-workspace handling fail-closed before any dispatch.

### Frontend
- [ ] Make command-mode UX reflect the classifier rather than inventing a separate assistant mode.
- [ ] Define `auto` as classifier-driven routing distinct from manual `chat` and `command` modes, or explicitly document it as a no-op fallback until the classifier changes land.
- [ ] Keep suggestions aligned with the real workspace gate and command surface.
- [ ] Ensure the submit flow does not drift into non-command conversational behavior.

### Verification
- [ ] Given `aos status`, the classifier resolves the same orchestrator path as direct command submission.
- [ ] Given ambiguous input, the user receives a clear clarification or help response.
- [ ] Given a failed backend execution, no UI branch implies the command succeeded.

## Phase 5: Loading, error, and startup diagnostics
**Goal:** make chat snapshot-load failures actionable without duplicating the turn-failure toast or bootstrap diagnostics that already exist.

**OpenSpec proposal:** one dedicated proposal for diagnostics and resiliency.

### Backend
- [ ] Preserve explicit endpoint, status, and recovery details in the chat API error shape.
- [ ] Keep workspace-not-found, validation, and transport errors distinct.
- [ ] Leave the existing turn-failure toast and bootstrap diagnostic flow intact.

### Frontend
- [ ] Replace the silent `loadSnapshot` catch with an explicit error state and retry path.
- [ ] Surface one actionable message when the snapshot cannot load.
- [ ] Distinguish 404, validation, CORS, and server failures in the UI path.
- [ ] Keep the page usable even when suggestions or quick actions cannot refresh.

### Verification
- [ ] Given a snapshot-load failure, the user sees a visible error state instead of silence.
- [ ] Given a failing request, the console and UI point to the real endpoint.
- [ ] Given repeated retries, the composer remains stable and the existing turn-failure toast behavior still works.

## Phase 6: Test coverage and end-to-end validation
**Goal:** lock the behavior down so future chat changes do not drift from the contract.

**OpenSpec proposal:** one dedicated proposal for test hardening.

### Backend
- [ ] Add coverage for workspace 404s, input normalization, and message-shape parity.
- [ ] Add coverage for history persistence and snapshot ordering.
- [ ] Add coverage for the event trail written during a turn.

### Frontend
- [ ] Add coverage for drawer behavior, artifact panel behavior, and role rendering.
- [ ] Add coverage for suggestion selection, quick actions, and composer state.
- [ ] Add coverage for loading and error states.

### Verification
- [ ] Given a real `aos status` command, the visible thread updates with the new response.
- [ ] Given the same workspace data, history, artifact detail, and command UX stay consistent.
- [ ] Given a chat-contract change, TypeScript and backend DTO tests fail before the UI drifts.

## Next Step
If you want, I can turn Phase 1 into the first OpenSpec proposal and start wiring the persistence model behind the current chat endpoint.
