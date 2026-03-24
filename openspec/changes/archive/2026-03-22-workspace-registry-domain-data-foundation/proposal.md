## Why

The frontend needs real, workspace-scoped data (workspaces, spec/state, and file browsing) so the launcher, dashboard, and file browser can operate against authoritative backend sources instead of placeholder/mock generators.

## What Changes

- Add a persistent workspace registry backed by SQLite, including workspace CRUD and status detection (e.g., whether `.aos/` exists).
- Stand up `nirmata.Api` domain endpoints for:
  - Workspace list/create/update/delete
  - Read-only Spec/State access (milestones, phases, tasks, project)
  - Workspace-scoped filesystem browsing and file content retrieval, gated to registered workspace roots
- Wire frontend workspace and filesystem views to the new domain endpoints (maintaining stable outward-facing hook shapes where applicable).

## Capabilities

### New Capabilities
- `workspace-registry`: Persist and manage registered workspaces (paths, IDs, status) and expose a service layer for domain endpoints.
- `workspace-domain-data`: Provide workspace-scoped domain read APIs (spec/state and filesystem access) for the frontend.

### Modified Capabilities
- `api`: Update/extend the domain API requirements to cover workspace CRUD, spec read endpoints, and workspace-scoped filesystem access (and align route shape with the domain API surface).

## Impact

- Backend:
  - New/updated domain services (`IWorkspaceService`, `IProjectService`, `ISpecService`, `IFileSystemService`) and controllers in `nirmata.Api`.
  - New persistence layer for workspace registry via SQLite (via `nirmata.Data`).
  - Additional request validation and path security constraints for filesystem endpoints.
- Frontend:
  - Replace remaining mock/placeholder sources used by workspace launcher/dashboard/file browser with real endpoint calls.
- Tests:
  - Update/add API tests for workspace CRUD, spec read, and filesystem gating behavior.
