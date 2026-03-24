## Context

- The system currently has two backend surfaces:
  - Domain data API (`src/nirmata.Api/Program.cs`) for projects/workspaces/phases/tasks/runs/issues/spec artifacts.
  - Daemon/engine API intended for local host/service lifecycle and engine connectivity.
- The frontend needs daemon/host features to always talk to a real local process (not MSW) so health polling, host console, and engine controls behave consistently across dev and production.
- We will establish `nirmata.Windows.Service.Api` (`src/nirmata.Windows.Service.Api/Program.cs`) as the authoritative daemon API surface.

## Goals / Non-Goals

**Goals:**

- Provide a single authoritative daemon API base URL consumed by the frontend (env-driven; default `http://localhost:9000`).
- Implement (or initially stub) the minimal daemon endpoints needed by the frontend:
  - `GET /api/v1/health`
  - `GET /api/v1/service`
  - `PUT /api/v1/service/host-profile`
  - `POST /api/v1/service/start`, `POST /api/v1/service/stop`, `POST /api/v1/service/restart`
- Enable development ergonomics:
  - Run `nirmata.Windows.Service.Api` as a console-hosted ASP.NET Core app.
  - Support CORS for the frontend dev origin.
- Define and document strict endpoint routing boundaries:
  - “daemon/engine/service host” features route to `nirmata.Windows.Service.Api`.
  - “domain data” features route to `nirmata.Api`.
- Replace remaining daemon-side frontend mocks with real endpoint wiring.

**Non-Goals:**

- Building a full production-grade Windows Service installer or service management UI.
- Implementing the full engine orchestration surface beyond the minimal endpoints needed for wiring and routing.
- Changing domain data API contracts beyond what is needed to stop using daemon mocks.

## Decisions

- Daemon API is a separate surface from the domain data API.
  - Rationale: Avoid coupling “service host lifecycle + engine connectivity” to domain data concerns.
  - Alternative considered: single API hosting both domain and daemon features. Rejected due to unclear ownership boundaries and higher blast radius.

- Frontend uses a single env-driven base URL for daemon API.
  - Default: `http://localhost:9000`.
  - Rationale: Keeps frontend wiring stable and allows swapping between dev console host and production companion process.
  - Alternative considered: derive daemon base URL from domain API host. Rejected because daemon may be separate process/port and should be independently configurable.

- Development hosting model: daemon API runs as a normal console app.
  - Rationale: Enables rapid iteration and debugging without Windows Service install/start/stop overhead.
  - Production model remains flexible (in-proc with the service worker or companion process).

- Routing rule is explicit and enforced at the hook/API-client layer.
  - Rationale: Prevents accidental “wrong surface” calls and keeps ownership clear.

## Risks / Trade-offs

- Risk: Endpoint ownership confusion between `nirmata.Api` and `nirmata.Windows.Service.Api`.
  - Mitigation: Maintain a small routing map doc and ensure each frontend hook/client has a single configured base URL per surface.

- Risk: Local environment port conflicts on 9000.
  - Mitigation: Make port/base URL env-driven with a documented default.

- Risk: CORS misconfiguration blocks frontend during dev.
  - Mitigation: Explicitly configure allowed origins in daemon API for known dev origins; validate with a minimal health poll from the frontend.

- Risk: Service lifecycle endpoints are initially stubs, leading to UI expectations mismatch.
  - Mitigation: Return explicit status payloads (e.g., “NotImplemented”) but keep shapes stable; add follow-on tasks to implement real behavior.
