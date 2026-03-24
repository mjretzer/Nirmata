## 1. Backend: workspace state and evidence services

- [x] 1.1 Add `IStateService` + `StateService` in `nirmata.Api` to read `.aos/state/state.json` into continuity state DTOs.
- [x] 1.2 Extend the state service to read `.aos/state/handoff.json` as an optional snapshot and tail `.aos/state/events.ndjson` as recent events.
- [x] 1.3 Add `IEvidenceService` + `EvidenceService` in `nirmata.Api` to list `.aos/evidence/runs/**` and read a single run folder with artifacts/logs.
- [x] 1.4 Add checkpoint read support for `.aos/state/checkpoints/**` and context-pack summaries under the state root.
- [x] 1.5 Register the new services in the domain API composition root.

## 2. Backend: workspace-scoped controllers

- [x] 2.1 Add `StateController` with `GET /v1/workspaces/{wsId}/state`, `/handoff`, and `/events`.
- [x] 2.2 Add `CheckpointsController` with `GET /v1/workspaces/{wsId}/checkpoints`.
- [x] 2.3 Add `RunsController` with `GET /v1/workspaces/{wsId}/runs` and `GET /v1/workspaces/{wsId}/runs/{runId}`.
- [x] 2.4 Add `ContextPacksController` with `GET /v1/workspaces/{wsId}/state/packs`.
- [x] 2.5 Add DTOs for state, handoff, events, checkpoints, runs, and context packs.

## 3. Frontend: replace continuity/checkpoint/run mocks

- [x] 3.1 Update `useContinuityState` in `nirmata.frontend/src/app/hooks/useAosData.ts` to consume the real state, handoff, events, and packs endpoints.
- [x] 3.2 Update `useCheckpoints` to consume the real workspace-scoped checkpoints endpoint.
- [x] 3.3 Update `useRuns` to consume the real workspace-scoped runs endpoint and optional task filtering.
- [x] 3.4 Keep `ContinuityPage` and `RunsPage` rendering the same user-facing layout while swapping in live data.

## 4. Validation

- [x] 4.1 Add or update backend tests for state, handoff, events, checkpoints, runs, and packs endpoints.
- [x] 4.2 Add or update frontend hook tests for continuity, checkpoints, and runs data loading.
- [x] 4.3 Verify `ContinuityPage` shows real cursor, events, handoff, and packs from `.aos/state/`.
- [x] 4.4 Verify `RunsPage` shows real workspace run history from `.aos/evidence/runs/`.
- [x] 4.5 Verify pause/resume behavior reflects whether `handoff.json` exists.
