## Why

The frontend needs a single authoritative “daemon/engine” backend surface (rather than MSW mocks) so host/service lifecycle and engine connectivity features always talk to a real local process.

This change establishes the Windows Service + companion daemon API (`nirmata.Windows.Service.Api`) as the canonical local endpoint surface and documents strict routing boundaries between the domain data API vs daemon API.

## What Changes

- Stand up `nirmata.Windows.Service.Api` as the authoritative daemon surface used by the frontend.
- Define and enforce an endpoint routing rule:
  - Domain data features route to `nirmata.Api`.
  - Daemon/engine/service host features route to `nirmata.Windows.Service.Api`.
- Ensure the daemon API is dev-friendly:
  - Runs as a normal console-hosted ASP.NET Core app during development.
  - Supports production hosting alongside the Windows Service (in-proc or companion process).
- Ensure a single env-driven base URL for the daemon API (default `http://localhost:9000`).
- Ensure CORS support for the frontend dev origin.
- Ensure the daemon API exposes (even if initially stubbed) minimal service lifecycle endpoints:
  - `GET /api/v1/health`
  - `GET /api/v1/service`
  - `PUT /api/v1/service/host-profile`
  - `POST /api/v1/service/start`, `POST /api/v1/service/stop`, `POST /api/v1/service/restart`
- Replace remaining daemon-side frontend mocks by wiring hooks to daemon endpoints.

## Capabilities

### New Capabilities

- *(none)*

### Modified Capabilities

- `daemon`: Expand/clarify daemon API requirements to cover service lifecycle status/control, base URL conventions, dev hosting mode, and CORS expectations.

## Impact

- Backend:
  - `src/nirmata.Windows.Service.Api/Program.cs` becomes a primary local API surface for host/engine features.
  - `src/nirmata.Windows.Service/Program.cs` (Worker) becomes the real service entry point in production.
- Frontend:
  - Any host/service lifecycle, engine health polling, and daemon connection UI must target the daemon API base URL.
  - Remaining MSW mocks for daemon behavior are replaced by real calls.
- Deployment/Configuration:
  - New/standardized environment configuration for daemon API base URL and CORS.
