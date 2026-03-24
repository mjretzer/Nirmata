## 1. Data layer: workspace registry persistence

- [x] 1.1 Define workspace registry data model — `Workspace` entity already exists (`Entities/Workspaces/Workspace.cs`: `Id`, `Path`, `Name`, `LastOpenedAt`, `LastValidatedAt`, `HealthStatus`)
- [x] 1.2 EF Core migrations already exist — `AddWorkspacesTable` + `AddLastValidatedAtToWorkspace`; `nirmataDbContext.Workspaces` DbSet is registered
- [x] 1.3 `IWorkspaceRepository` + `WorkspaceRepository` already implemented with full CRUD
- [x] 1.4 Add basic unit tests for workspace registry persistence behavior

## 2. Domain services: workspace resolution and status

- [x] 2.1 Add `IWorkspaceService` + `WorkspaceService` to manage registry operations and resolve workspace roots
- [x] 2.2 Implement workspace status derivation (exists, accessible, `.aos/` presence) for `WorkspaceSummary.status`
- [x] 2.3 Add validation for workspace paths (absolute path required, normalization, basic existence checks)
- [x] 2.4 Register `IWorkspaceRepository`, `IWorkspaceService`, `ISpecService`, and `IFileSystemService` in `nirmata.Api` DI container (`Program.cs` and/or `ServiceCollectionExtensions`); note `IWorkspaceRepository` exists but is not currently registered

## 3. API: workspace CRUD endpoints

- [x] 3.1 Create `WorkspacesController` with `GET /v1/workspaces` and `GET /v1/workspaces/{workspaceId}`
- [x] 3.2 Add `POST /v1/workspaces` to register a workspace and return created workspace
- [x] 3.3 Add `PUT /v1/workspaces/{workspaceId}` to update registered workspace root path
- [x] 3.4 Add `DELETE /v1/workspaces/{workspaceId}` to deregister a workspace
- [x] 3.5 Add request/response DTOs for workspace create/update/detail/summary

## 4. Spec/state read layer (read-only pass 1)

- [x] 4.1 Implement `ISpecService` to read workspace spec/state sources and map to API DTOs
- [x] 4.2 Define response DTO shapes for spec endpoints (milestones/phases/tasks/project) and align them to the AOS artifact schemas
- [x] 4.3 Add `GET /v1/workspaces/{workspaceId}/spec/milestones`
- [x] 4.4 Add `GET /v1/workspaces/{workspaceId}/spec/phases`
- [x] 4.5 Add `GET /v1/workspaces/{workspaceId}/spec/tasks`
- [x] 4.6 Add `GET /v1/workspaces/{workspaceId}/spec/project`

## 5. Workspace-scoped filesystem service and endpoints

- [x] 5.1 Implement `IFileSystemService` with strict workspace-root path gating
- [x] 5.2 Ensure path normalization uses full-path resolution and rejects escape attempts outside workspace root (normalize URL paths as forward-slash and convert to OS separators before `Path.GetFullPath` + containment checks on Windows)
- [x] 5.3 Add `GET /v1/workspaces/{workspaceId}/files/{*path}` to return `DirectoryListingResponse` for directories and raw file content for files (DTO shape defined in `specs/workspace-domain-data/spec.md`)
- [x] 5.4 Add content-type handling and size limits for file reads (initial sensible defaults)

## 6. API tests and security hardening

- [x] 6.1 Add API tests for workspace CRUD behavior (create/list/get/update/delete)
- [x] 6.2 Add API tests for workspace ID validation (404 for unknown workspace)
- [x] 6.3 Add API tests for filesystem gating (path traversal and escape attempts rejected)
- [x] 6.4 Add API tests for `.aos/` presence affecting workspace status

## 7. Frontend wiring (replace placeholders with real data)

- [x] 7.1 Update `domainClient` methods in `apiClient.ts` to use workspace-scoped URLs (`/v1/workspaces/{workspaceId}/spec/milestones`, `/phases`, `/tasks`, `/project`) replacing the current global placeholder paths
- [x] 7.2 Replace `useWorkspace` / `useWorkspaces` stub calls with real `domainClient.getWorkspace` / `domainClient.getWorkspaces` fetch calls hitting `/v1/workspaces` and `/v1/workspaces/{id}`
- [x] 7.3 Replace `usePhases`, `useMilestones`, `useTasks`, and `useProjectSpec` endpoint targets with workspace-scoped URLs; thread `activeWorkspaceId` from `WorkspaceContext` as an internal dependency (add to each hook's `useEffect` dep array; skip fetch when `activeWorkspaceId` is empty)
- [x] 7.4 Replace `useFileSystem` stub with real calls to `/v1/workspaces/{workspaceId}/files/{*path}`; detect directory vs file response via `Content-Type`; rename `FilesystemNode.kind` → `type` to match `DirectoryEntry` DTO; needed for `WorkspacePathPage`, `PlanPage`, `CodebasePage`
- [x] 7.5 Wire `WorkspaceLauncherPage` to list/register workspaces via `/v1/workspaces` (POST to register, GET to list)
- [x] 7.6 Wire `WorkspaceDashboard` to consume real workspace + spec endpoints (depends on 7.2, 7.3)
- [x] 7.7 Verify outward-facing hook return shapes are unchanged for existing consumers; confirm `activeWorkspaceId` switching triggers correct re-fetch across all mounted hooks

## 8. Operational polish

- [x] 8.1 Add structured logging for workspace resolution failures and filesystem rejections
- [x] 8.2 Ensure error responses are consistent (validation errors, not-found, forbidden/path rejected)
- [x] 8.3 Update any developer docs needed to run `nirmata.Api` with a local SQLite database
- [x] 8.4 Configure CORS in `nirmata.Api/Program.cs` — add a named policy allowing the frontend dev origin (`http://localhost:5173`); do not use `AllowAnyOrigin`

## Verification

- All workspace list, dashboard, and plan pages render real `.aos/` data
- File browser navigates real workspace file tree
- Workspace CRUD (register, update path, deregister) round-trips correctly through the registry
- Path traversal attempts (e.g. `../`, `%2F..`) are rejected with a non-2xx response
- Switching `activeWorkspaceId` causes all mounted data hooks to re-fetch against the new workspace
- `npm run typecheck && npm run build` pass
