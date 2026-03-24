## Why

The daemon-side frontend experience still depends on mock or stubbed behavior, so health checks, service status, command execution, logs, and diagnostics are not yet backed by the real local API surface. This change completes the Phase 4 daemon contract so the frontend can talk to a stable `nirmata.Windows.Service.Api` endpoint instead of placeholder data.

## What Changes

- Finish the daemon HTTP surface in `nirmata.Windows.Service.Api` for health, service status, host profile updates, command execution, runs, logs, and diagnostics.
- Align the daemon response shapes used by the frontend, including health and service status payloads.
- Replace daemon-side frontend mocks and stub hooks with real fetches against the configured daemon base URL.
- Keep the daemon API as a separate local surface with an env-driven base URL defaulting to `http://localhost:9000`.

## Capabilities

### New Capabilities

- *(none)*

### Modified Capabilities

- `daemon`: Expand the daemon HTTP API contract to cover the full Phase 4 surface, including health, service status, host profile, command execution, run history, logs, and diagnostics.

## Impact

- Backend:
  - `nirmata.Windows.Service.Api` controllers for health, service, commands, runs, logs, and diagnostics.
  - Any backing state or runtime service needed to produce real status, log, and diagnostics payloads.
- Frontend:
  - `useHostConsole`, `useAosCommand`, `useEngineConnection`, `useDiagnostics`, and `WorkspaceContext` health polling.
  - Daemon routing config driven by `VITE_DAEMON_URL`.
- Tests:
  - API and hook tests covering the new daemon response shapes and endpoint wiring.
