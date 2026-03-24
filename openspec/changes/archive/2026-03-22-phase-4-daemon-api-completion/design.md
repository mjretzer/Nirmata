## Context

The daemon-facing frontend code still mixes mocked behavior, stale endpoint shapes, and partially wired client calls. Phase 4 is about finishing the daemon API as a real local surface so host/service UX, command execution, health polling, and diagnostics all target one authoritative process.

The current daemon controller set already exposes some endpoints, but the contract is incomplete and does not yet match the Phase 4 roadmap:

- Health currently returns a latency-oriented payload, while the roadmap expects uptime-oriented health reporting.
- Service status needs to include surface information consumed by the host console.
- Command execution needs to return a simple `{ ok, output }` payload.
- Logs and diagnostics still need real query and cleanup endpoints.
- Frontend hooks still rely on mock/stub assumptions in several places.

Constraints:
- The daemon API remains a separate surface from the domain data API.
- The frontend must continue using an env-driven daemon base URL with a development default of `http://localhost:9000`.
- The daemon API should stay development-friendly and runnable as a normal console-hosted ASP.NET Core app.

## Goals / Non-Goals

**Goals:**
- Complete the daemon HTTP surface for health, service status, host profile, commands, runs, logs, and diagnostics.
- Standardize the daemon response shapes used by the frontend.
- Wire the frontend daemon hooks to real network calls against `VITE_DAEMON_URL`.
- Keep service/host/engine responsibilities separate from the domain data API.

**Non-Goals:**
- Reworking the domain data API surface for workspaces/spec/tasks/runs/issues.
- Building a full Windows Service installer or orchestration UI.
- Implementing a large observability backend beyond the data required by the current frontend views.

## Decisions

- Use the daemon API as the single source of truth for host/service/engine UX.
  - Rationale: the frontend already treats daemon features as a separate surface, and the Phase 4 roadmap explicitly routes those requests away from `nirmata.Api`.
  - Alternative considered: fold daemon features into the domain API. Rejected because it blurs ownership boundaries and increases coupling.

- Keep the daemon health response compact and uptime-based.
  - Rationale: the roadmap and frontend health polling need a simple availability signal plus a durable uptime value, not a latency probe.
  - Alternative considered: keep the current latency-oriented payload. Rejected because it does not match the Phase 4 contract.

- Return service surfaces as part of the service status payload.
  - Rationale: the host console needs status and surface metadata together, and keeping them on one endpoint avoids a second fetch during rendering.
  - Alternative considered: keep separate host-surface endpoints. Rejected because Phase 4 consolidates the surface data into the service status response.

- Prefer JSON polling for logs before introducing streaming complexity.
  - Rationale: the roadmap allows polling or SSE, and polling is easier to wire into the current hooks with minimal behavioral risk.
  - Alternative considered: implement SSE first. Rejected for this phase because it adds extra client/server plumbing without a clear user-facing gain.

- Keep diagnostics cleanup explicit and narrow.
  - Rationale: deleting locks and cache entries should be separate, auditable actions rather than a broad reset endpoint.
  - Alternative considered: provide a single destructive cleanup endpoint. Rejected because it is harder to reason about and test.

## Risks / Trade-offs

- [Risk] Response-shape mismatch between frontend hooks and backend controllers. → Mitigation: define DTOs centrally and update the hook types/tests alongside the controllers.
- [Risk] Log and diagnostics endpoints may initially be backed by in-memory or process-local state. → Mitigation: keep the public contract stable and isolate the storage mechanism behind a service layer.
- [Risk] Changing the health payload from latency to uptime could break assumptions in existing UI code. → Mitigation: update the health poll consumer at the same time and keep the response shape minimal.
- [Risk] Separate daemon and domain APIs can still be confused at the call site. → Mitigation: keep the routing map explicit and use `VITE_DAEMON_URL` consistently for daemon features.

## Migration Plan

1. Update the daemon API contracts and controllers to the Phase 4 shapes.
2. Wire the frontend daemon hooks to the new endpoints and response DTOs.
3. Update tests to cover the new daemon payloads and cleanup actions.
4. Validate the daemon API locally with the frontend dev server against the configured daemon base URL.

Rollback strategy:
- If any new endpoint shape destabilizes the frontend, revert the hook wiring first while keeping the backend contract in place.
- If the backend contract itself causes regressions, restore the previous payload shape and re-run the frontend migrations after the contract is stabilized.

## Open Questions

- Should logs be returned as finite polling snapshots only, or should the backend also expose an SSE variant in the same phase?
- What is the authoritative source for service surface metadata at runtime: the Windows Service host, the daemon process, or a shared runtime model?
- Should diagnostics cleanup actions return the deleted item count, the remaining count, or only `{ ok: true }`?
