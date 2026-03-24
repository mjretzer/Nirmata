## Context

The frontend needs real, workspace-scoped data for the workspace launcher, dashboard, and file browser. Today, these experiences are blocked or rely on placeholder/mock data because there is no authoritative domain API to:

- Register and persist the set of known workspaces
- Determine workspace status (e.g., whether it is initialized and has a `.aos/` directory)
- Read workspace spec artifacts for milestones/phases/tasks/project (Phase 1; `.aos/spec/*`)
- Browse and read files within a workspace root safely

This change introduces a minimal but coherent domain-data foundation in `nirmata.Api`, backed by a SQLite workspace registry (via `nirmata.Data`), and a set of read-only workspace endpoints designed for frontend consumption.

Constraints:
- File access must be strictly scoped to registered workspace roots.
- Endpoint route shapes must align with the domain API surface (not the daemon API surface).
- The initial pass is read-only for spec/state, but must be structured to allow future expansion.

## Goals / Non-Goals

**Goals:**
- Provide a persistent workspace registry with CRUD operations.
- Surface a `WorkspaceSummary.status` derived from workspace root inspection (including `.aos/` presence).
- Provide domain endpoints to:
  - List/create/update/delete workspaces
  - Read workspace spec slices (milestones, phases, tasks, project) from `.aos/spec/*`
  - Browse a workspace-scoped filesystem and read file content
- Enforce path gating so the filesystem endpoints can only access paths under a registered workspace root.

**Non-Goals:**
- Implement full spec authoring/editing via the API.
- Implement write support for the workspace filesystem endpoints.
- Expose `.aos/state/*` artifacts (e.g., `state.json`, `events.ndjson`) via domain endpoints in Phase 1.
- Define the final, complete domain API surface for all AOS objects (runs, issues, checkpoints, etc.).
- Implement complex workspace discovery heuristics beyond explicit registration.

## Decisions

- Use SQLite as the persistence layer for the workspace registry via EF Core using the existing `nirmata.Data` patterns.
  - Rationale: the repo already uses EF Core with SQLite (`nirmataDbContext` in `nirmata.Data`, DI registration in `nirmata.Api`) and this keeps persistence consistent (entities + migrations + repositories).
  - Approach: add/update a workspace registry entity in `nirmata.Data`, map it in `nirmataDbContext`, and create an EF Core migration to evolve the SQLite schema.
  - Alternatives: raw SQL / lightweight SQLite wrapper (simpler for a single table but diverges from existing patterns and increases maintenance surface).
  - **Note:** The `Workspace` entity, `IWorkspaceRepository`, `WorkspaceRepository`, `nirmataDbContext.Workspaces` DbSet, and migrations (`AddWorkspacesTable`, `AddLastValidatedAtToWorkspace`) already exist. The remaining data-layer work is limited to DI registration and any field additions required by the DTO shape.

- Treat workspace identity as an API-level stable ID independent of path.
  - Rationale: paths can change; a stable ID decouples frontend state and enables path updates.
  - Implementation detail: generate IDs server-side at create time; allow path update via `PUT`.

- Centralize workspace root validation and path gating in a dedicated service.
  - Rationale: filesystem safety is cross-cutting and must not be duplicated across controllers.
  - Approach: `IWorkspaceService` resolves workspace root, and `IFileSystemService` enforces that any requested path is within that root after normalization.

- Keep spec/state APIs read-only and workspace-scoped.
  - Rationale: aligns with Phase 3 goal of real data for UI with minimal risk.
  - Approach: Phase 1 is spec-only: `ISpecService` reads structured outputs from `.aos/spec/*` and maps them to DTOs consumed by the UI. `.aos/state/*` (current execution position, blockers, decisions, event stream) is intentionally out of scope for Phase 1 and will be added when the dashboard needs authoritative runtime state.

- Route shape: use `/v1/...` for domain endpoints, distinct from daemon endpoints.
  - Rationale: preserves the routing boundary and avoids coupling to daemon API base URLs.

- Workspace-ID threading: data hooks (`useMilestones`, `usePhases`, `useTasks`, `useProjectSpec`, `useFileSystem`) read `activeWorkspaceId` from `WorkspaceContext` internally rather than accepting it as a parameter.
  - Rationale: keeps the outward-facing hook signatures stable for existing consumers; `WorkspaceContext.activeWorkspaceId` is already available app-wide. `activeWorkspaceId` must be a `useEffect` dependency so hooks re-fetch when the active workspace changes.
  - Approach: each hook calls `useWorkspaceContext()` and includes `activeWorkspaceId` in its effect dependency array; when `activeWorkspaceId` is empty the hook stays idle (no request fired).

- `WorkspaceSummary.status` is derived from filesystem inspection on each read; the existing `Workspace.HealthStatus` stored column is not used for this purpose.
  - Rationale: the spec requires live status derived from `.aos/` presence; a stale stored value is misleading. `HealthStatus` will be ignored by the service layer for now and may be removed in a subsequent migration once the derived-status pattern is validated.

- `WorkspaceSummary.lastModified` maps to the entity's `LastOpenedAt` field (nullable, coerced to `DateTimeOffset.MinValue` when null).
  - Rationale: avoids an unnecessary migration for an informational display field. If a true `UpdatedAt` timestamp is needed in the future, a migration can add it then.

- CORS must be configured in `nirmata.Api` for local development.
  - Rationale: `nirmata.Api` runs on a different port from the Vite dev server; without `AddCors` / `UseCors` the browser will block all domain API calls silently.
  - Approach: add a named CORS policy in `Program.cs` that allows the frontend dev origin (`http://localhost:5173`). Keep the policy strict — allow only the frontend origin, not `*`.

- File endpoint dual-response handling on the frontend: the `domainClient.getFilesystem()` method detects whether the response is a directory listing or raw file bytes by inspecting the `Content-Type` response header (JSON → directory, otherwise → file).
  - Rationale: the same route returns different content types; the client must branch on the header rather than the URL.
  - Note: the existing `FilesystemNode.kind` field in `apiClient.ts` must be renamed to `type` to align with the `DirectoryEntry` DTO shape defined in `specs/workspace-domain-data/spec.md`.

## Risks / Trade-offs

- [Path traversal / unsafe file access] → Normalize paths, enforce `Path.GetFullPath` containment within registered workspace root, and reject symlinks/junction escapes where applicable.
- [Registry drift when workspace is deleted/moved] → Surface status fields and allow update/deregister operations; avoid implicit filesystem discovery in this phase.
- [Spec/state file format uncertainty] → Implement `ISpecService` with a narrow set of DTOs and explicit error reporting; keep format parsing isolated and testable.
- [API route mismatch with existing `openspec/specs/api`] → Provide a delta spec that clarifies the domain API route shape and the required endpoints.
- [Hook re-fetch on workspace switch] → Because hooks depend on `activeWorkspaceId`, switching the active workspace triggers a re-fetch across every mounted hook simultaneously. This is acceptable for Phase 1 given the small data volumes, but may need debouncing or suspense coordination later.
- [CORS misconfiguration] → An overly permissive policy (`AllowAnyOrigin`) would be a security regression. Policy must enumerate allowed origins explicitly.
