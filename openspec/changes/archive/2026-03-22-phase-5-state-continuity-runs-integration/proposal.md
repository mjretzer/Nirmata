## Why

Phase 5 on the roadmap still relies on mocked continuity, checkpoint, and run data. The UI needs authoritative AOS workspace state so `ContinuityPage`, `RunsPage`, and checkpoint views can reflect the actual workspace files under `.aos/state/` and `.aos/evidence/runs/` instead of placeholder data.

## What Changes

- Add domain services in `nirmata.Api` to read workspace state artifacts from disk:
  - `.aos/state/state.json` → continuity state
  - `.aos/state/handoff.json` → optional handoff snapshot
  - `.aos/state/events.ndjson` → recent event stream
  - `.aos/state/checkpoints/**` → checkpoint summaries
  - `.aos/evidence/runs/**` → run history and run details
- Add workspace-scoped API controllers for:
  - `GET /v1/workspaces/:wsId/state`
  - `GET /v1/workspaces/:wsId/state/handoff`
  - `GET /v1/workspaces/:wsId/state/events`
  - `GET /v1/workspaces/:wsId/checkpoints`
  - `GET /v1/workspaces/:wsId/runs`
  - `GET /v1/workspaces/:wsId/runs/:runId`
  - `GET /v1/workspaces/:wsId/state/packs`
- Replace frontend continuity, checkpoint, and run hooks with real domain API calls while keeping the page-level UX stable.

## Capabilities

### New Capabilities

- `state-domain`: Read and expose workspace continuity state, handoff, events, and context packs.
- `runs-domain`: Read and expose workspace run history and run detail records from AOS evidence.
- `checkpoints-domain`: Read and expose checkpoint summaries from AOS state artifacts.

### Modified Capabilities

- `api`: Extend the domain API surface to cover workspace-scoped state, checkpoints, runs, and context packs.
- `frontend`: Replace mock continuity, checkpoints, and runs data with live workspace-scoped API responses.

## Impact

- Backend:
  - New `nirmata.Api` services and controllers for state, checkpoints, runs, and context packs.
  - DTOs that map raw AOS files into stable API payloads.
- Frontend:
  - `useContinuityState`, `useCheckpoints`, and `useRuns` will switch from mock data to live API requests.
  - `ContinuityPage` and `RunsPage` will render actual workspace state instead of static placeholders.
- Tests:
  - API tests for state, handoff, events, checkpoints, runs, and packs endpoints.
  - Hook tests for the updated continuity/checkpoints/runs data flows.
