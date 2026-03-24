## Context

Phase 5 closes the loop between the AOS workspace files and the app shell. The repository already has public AOS contracts in `nirmata.Aos.Public` for state, checkpoints, and runs, and the frontend already contains hooks and pages that expect continuity, checkpoint, and run data. What is missing is the authoritative domain API layer in `nirmata.Api` that reads real workspace files and exposes them through workspace-scoped endpoints.

The current frontend wiring already points at domain helpers such as `getContinuity()`, `getCheckpoints()`, and `getRuns()`, but those calls still need to target the Phase 5 workspace-scoped contract and map the actual filesystem-backed payloads into the UI models.

Constraints:
- Keep this work in the domain API, not the daemon API.
- Use workspace-scoped routes under `/v1/workspaces/{workspaceId}/...` so the data follows the active workspace.
- Treat state, checkpoints, runs, and context packs as read-only for this phase.
- Keep response DTOs narrow and stable so the frontend can map them without needing raw file formats.

## Goals / Non-Goals

**Goals:**
- Read continuity state from `.aos/state/state.json`.
- Read handoff state from `.aos/state/handoff.json` when present.
- Read the event tail from `.aos/state/events.ndjson`.
- Read checkpoint summaries from `.aos/state/checkpoints/**`.
- Read run history and run detail data from `.aos/evidence/runs/**`.
- Expose a context-pack endpoint that surfaces the packs the continuity page needs.
- Wire the frontend hooks to the new endpoints.

**Non-Goals:**
- Write/update/delete state, checkpoints, or runs through the API.
- Redesign the AOS file formats.
- Merge daemon API endpoints into the domain surface.
- Build an SSE/event-streaming system for the event tail in this phase.

## Decisions

- Keep the domain API separate from `nirmata.Windows.Service.Api`.
  - Rationale: the daemon surface is for host/service UX, while Phase 5 is about workspace-scoped AOS data.

- Use a small set of focused DTOs for continuity, checkpoints, and runs.
  - Rationale: the frontend only needs stable summary objects, not raw filesystem metadata.
  - Alternative considered: expose the on-disk JSON directly. Rejected because it couples the UI to storage details.

- Model handoff as an optional resource.
  - Rationale: `handoff.json` may not exist for every workspace, so `404`/`null` semantics are a better fit than forcing an empty snapshot.

- Keep event retrieval as a bounded tail query.
  - Rationale: the continuity page only needs recent events, and loading the full NDJSON file would be unnecessary and more fragile.

- Surface runs through the workspace-scoped domain API even though the daemon also has a run concept.
  - Rationale: the roadmap explicitly distinguishes workspace evidence from daemon runtime runs, and the frontend pages should stay aligned to the AOS workspace data.

- Map checkpoints and context packs from the same workspace state root.
  - Rationale: the continuity page needs a single source for the state story, even when the underlying artifacts live in separate folders.

## Risks / Trade-offs

- [Risk] Route overlap or confusion with the existing daemon `api/v1/runs` endpoint. → Mitigation: keep domain endpoints workspace-scoped and route them through `nirmata.Api` only.
- [Risk] The exact on-disk layout for packs or checkpoints may evolve. → Mitigation: isolate parsing in services and keep DTOs stable.
- [Risk] Frontend hook assumptions may still reflect mock-era shapes. → Mitigation: update the hooks and tests together with the controllers.
- [Risk] Missing files are expected for some workspaces. → Mitigation: handle absent `handoff.json`, empty checkpoints, and empty run histories gracefully.

## Migration Plan

1. Add domain services for state, checkpoints, runs, and context packs.
2. Add workspace-scoped controllers for the new API surface.
3. Update frontend hooks to consume the live endpoints.
4. Verify the continuity and runs pages against a real workspace with AOS artifacts.

Rollback strategy:
- Revert the frontend hook wiring first if DTO mapping mismatches appear.
- If service parsing proves unstable, keep the controller contract and tighten the parser in the service layer without changing the endpoint surface.
