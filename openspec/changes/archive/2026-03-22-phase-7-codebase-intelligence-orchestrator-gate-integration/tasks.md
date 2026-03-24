## 1. Backend: codebase intelligence

- [x] 1.1 Inventory the workspace codebase pack format before coding. Verify the exact `.aos/codebase/` inputs used by the backend: manifest/hash files, artifact payload files, `map.json`, and `stack.json`. Resolve the workspace root from the workspace id only; do not read global state.
- [x] 1.2 Implement `ICodebaseService` and `CodebaseService` to:
  - list artifacts for `GET /v1/workspaces/{workspaceId}/codebase`
  - return one artifact for `GET /v1/workspaces/{workspaceId}/codebase/{artifactId}`
  - classify status exactly as `ready` = file exists and hash matches, `stale` = file exists and hash differs, `missing` = recognized artifact absent, `error` = unreadable or unparseable file
  - return `404 Not Found` for unknown workspaces and unsupported artifact ids
  - include stable `id`, `type`, `status`, `path`, and `lastUpdated` fields for every artifact
  - include `languages` from `map.json` and `stack` from `stack.json` in the inventory payload
- [x] 1.3 Add workspace-scoped codebase controller routes in the API project and register the service in dependency injection. Keep the controller under `api/v1` and do not expose a global codebase endpoint.

## 2. Backend: orchestrator gate

- [x] 2.1 Implement `IOrchestratorGateService` and `OrchestratorGateService` to derive the next gate from workspace cursor, task plan order, UAT state, evidence state, and dependency state.
- [x] 2.2 Make the gate response explicit and stable. It must contain `taskId`, `taskTitle`, `runnable`, `recommendedAction`, and `checks[]`. Each check must contain `id`, `kind`, `label`, `detail`, and `status`. Use the existing API check statuses: `pass`, `fail`, `warn`.
- [x] 2.3 Add workspace-scoped endpoints:
  - `GET /v1/workspaces/{workspaceId}/orchestrator/gate`
  - `GET /v1/workspaces/{workspaceId}/orchestrator/timeline`
  Return timeline steps in order, each with `id`, `label`, and `status`.
- [x] 2.4 Return `404 Not Found` for unknown workspaces and register the orchestrator gate service in dependency injection.

## 3. Frontend: API wiring

- [x] 3.1 Update `nirmata.frontend/src/app/utils/apiClient.ts` to call the new workspace-scoped codebase and orchestrator endpoints. Add or adjust DTOs so the client types match the backend responses exactly.
- [x] 3.2 Keep the exported hook names and external shapes stable in `nirmata.frontend/src/app/hooks/useAosData.ts`:
  - `useCodebaseIntel()`
  - `useOrchestratorState()`
  Read `activeWorkspaceId` from `useWorkspaceContext()`. If it is missing, return empty/default state and make no request. Map the workspace-scoped responses into the existing frontend types used by the pages.
- [x] 3.3 Update `nirmata.frontend/src/app/pages/CodebasePage.tsx` so it renders the real artifact inventory, language breakdown, and stack data from `useCodebaseIntel()`.
- [x] 3.4 Update `nirmata.frontend/src/app/pages/WorkspaceDashboard.tsx` so the next-step card uses the real orchestrator gate response as the source of truth. Remove local gate derivation from the render path; keep only loading/error fallbacks if needed.
- [x] 3.5 Leave the no-workspace routes and page layout unchanged unless the new data shape forces a change.

## 4. Verification

- [x] 4.1 Add backend tests for codebase inventory, artifact detail, readiness classification, and unknown-workspace / unknown-artifact 404s.
- [x] 4.2 Add backend tests for gate derivation, check content, recommended action, and timeline ordering.
- [x] 4.3 Add frontend tests for hook mapping and page rendering with the new API payloads.
- [x] 4.4 Run the relevant backend and frontend test suites and confirm the workspace-scoped endpoints and dashboard states match the OpenSpec requirements.
