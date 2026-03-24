# Nirmata Project Roadmap

## Business Logic & API Catalog
This section defines the authoritative feature surface and interface contracts required by the frontend, derived from the `useAosData.ts` hook surface and mock data layers.

Routing convention: `nirmata.Windows.Service.Api` uses `/api/v1/*` routes. `nirmata.Api` uses `/v1/*` for domain data routes (and also exposes health via `/api/health` and `/health`).

### A. nirmata.Windows.Service.Api — Daemon/Engine Boundary
These power the features that currently live in `WorkspaceContext`, `HostConsolePage`, `DiagnosticsPage`, and the engine settings tabs.

| Feature | Interface Contract | Method | Description |
| :--- | :--- | :--- | :--- |
| Daemon health poll | `GET /api/v1/health` | GET | Returns `{ ok, version, uptimeMs }` |
| Service status | `GET /api/v1/service` | GET | Returns `ServiceStatus + ApiSurface[]` |
| Host profile update | `PUT /api/v1/service/host-profile` | PUT | Sets `{ label, baseUrl, env }` |
| AOS command execution | `POST /api/v1/commands` | POST | Accepts `argv: string[]`, streams or returns `{ ok, output }` |
| Run listing (engine) | `GET /api/v1/runs` | GET | Returns `Run[]` scoped to the service host |
| Host log streaming | `GET /api/v1/logs` | GET | SSE or polling; returns `HostLogLine[]` |
| Diagnostics | `GET /api/v1/diagnostics` | GET | Returns `{ logs, artifacts, locks, cacheEntries }` |
| Lock management | `DELETE /api/v1/diagnostics/locks/:id` | DELETE | Force-releases a lock |
| Cache management | `DELETE /api/v1/diagnostics/cache/:scope` | DELETE | Prunes stale cache |

### B. nirmata.Api — Domain Data (AOS Workspace State)
These power `PlanPage`, `RunsPage`, `VerificationPage`, `FixPage`, `ContinuityPage`, `CodebasePage`, `WorkspaceDashboard`, and `WorkspaceLauncherPage`.

**Workspace**
- `GET /v1/workspaces` → `WorkspaceSummary[]`
- `GET /v1/workspaces/:id` → `Workspace`
- `POST /v1/workspaces` → create/register a workspace (path + alias)
- `PUT /v1/workspaces/:id` → update alias/pin
- `DELETE /v1/workspaces/:id` → deregister

**Spec: Milestones / Phases / Tasks**
- `GET /v1/workspaces/:wsId/spec/milestones` → `Milestone[]`
- `GET /v1/workspaces/:wsId/spec/phases[?milestoneId=]` → `Phase[]`
- `GET /v1/workspaces/:wsId/spec/phases/:id` → `Phase`
- `GET /v1/workspaces/:wsId/spec/tasks[?phaseId=&status=]` → `Task[]`
- `GET /v1/workspaces/:wsId/spec/tasks/:id` → `Task`
- `GET /v1/workspaces/:wsId/spec/tasks/:id/plan` → `TaskPlan`
- `GET /v1/workspaces/:wsId/spec/project` → `ProjectSpec`

**Runs / Evidence**
- `GET /v1/workspaces/:wsId/runs[?taskId=]` → `Run[]`
- `GET /v1/workspaces/:wsId/runs/:runId` → `Run`

**Issues / UAT**
- `GET /v1/workspaces/:wsId/issues[?status=&severity=]` → `Issue[]`
- `POST /v1/workspaces/:wsId/issues` → create issue
- `PUT /v1/workspaces/:wsId/issues/:id` → update issue status
- `GET /v1/workspaces/:wsId/uat` → UAT checklist + outcomes

**Continuity / State**
- `GET /v1/workspaces/:wsId/state` → `ContinuityState` (cursor + decisions + blockers)
- `GET /v1/workspaces/:wsId/state/handoff` → `HandoffSnapshot | null`
- `GET /v1/workspaces/:wsId/state/events[?limit=]` → `Event[]`
- `GET /v1/workspaces/:wsId/state/packs` → `ContextPack[]`
- `GET /v1/workspaces/:wsId/checkpoints` → `Checkpoint[]`

**Codebase Intelligence**
- `GET /v1/workspaces/:wsId/codebase` → `{ artifacts, languages, stack }`
- `GET /v1/workspaces/:wsId/codebase/:artifactId` → raw artifact JSON

**Orchestrator / Gate**
- `GET /v1/workspaces/:wsId/orchestrator/gate` → `NextTaskGate` (current task gate state)
- `GET /v1/workspaces/:wsId/orchestrator/timeline` → `TimelineStep[]`

**File System**
- `GET /v1/workspaces/:wsId/files/*` → `FileSystemNode` tree or raw file content

### C. Business Logic Services (nirmata.Services/)
The full set of service interfaces needed to back the API controllers:

- **IWorkspaceService**: Register/resolve workspace paths, scan `.aos/` presence, surface status.
- **IProjectService**: CRUD + search as specified in Phase 1 §2.
- **ISpecService**: Read/write milestones, phases, tasks, task plans from `.aos/spec/**`.
- **IStateService**: Read/write `state.json`, append events to `events.ndjson`, manage checkpoints.
- **IEvidenceService**: Read runs, artifacts, logs from `.aos/evidence/**`.
- **ICodebaseService**: Read codebase intelligence artifacts from `.aos/codebase/**`.
- **IIssueService**: CRUD for `.aos/spec/issues/**`, link to tasks/phases.
- **IUatService**: Read/write UAT records, derive pass/fail state.
- **IOrchestratorGateService**: Derive `NextTaskGate` from state + tasks + evidence.
- **IFileSystemService**: Traverse workspace directory, serve file content.
- **IDiagnosticsService**: Read locks, cache entries, logs, artifacts from the engine workspace.
- **ICommandService**: Dispatch `aos` CLI argv, capture stdout/stderr as structured result.

## Phase 1: Endpoint Reconciliation & API Creation
Status: In progress

### 1) Frontend scanning (authoritative list of required endpoints)

Start here (this is the current single “data layer”):

- `nirmata.frontend/src/app/hooks/useAosData.ts`
  - This file is intentionally written as the swap-point: it returns mock data today and is where real HTTP calls should be added so consumers don’t change.
  - It also documents intended endpoints in docstrings near the bottom:
    - `POST /api/v1/commands` (intended for `src/nirmata.Windows.Service.Api`)
    - `GET /api/v1/health` and `PUT /api/v1/service/host-profile` (intended for the Windows service/daemon API)

Where those hooks are used (scan these to determine *what UI needs*):

- `nirmata.frontend/src/app/pages/PlanPage.tsx` (Plan console / spec artifacts)
- `nirmata.frontend/src/app/pages/SettingsPage.tsx` (engine host profile, config, provider keys, git)
- `nirmata.frontend/src/app/pages/HostConsolePage.tsx` (service start/stop/restart, API surface reachability)
- `nirmata.frontend/src/app/router.tsx` (route map showing feature surface)

Mock/placeholder data sources to inventory (these define payload shapes the UI currently assumes):

- `nirmata.frontend/src/app/data/mockData.ts` (workspaces, phases, tasks, runs, issues, project spec)
- `nirmata.frontend/src/app/data/mockFileSystem.ts` (the “.aos/*” virtual file tree)
- `nirmata.frontend/src/app/data/mockContinuityData.ts`
- `nirmata.frontend/src/app/data/mockHostData.ts`
- `nirmata.frontend/src/app/data/mockOrchestratorData.ts`
- `nirmata.frontend/src/app/data/mockChatData.ts`

Environment/config points for URLs:

- `nirmata.frontend/src/app/context/WorkspaceContext.tsx`
  - Uses `import.meta.env.VITE_DAEMON_URL` (fallback `http://localhost:9000`) and polls:
    - `GET {VITE_DAEMON_URL}/api/v1/health`

Current routes in the repo today (so you don’t guess):

- Domain data API (`src/nirmata.Api`):
  - `GET /api/health` (controller: `src/nirmata.Api/Controllers/HealthController.cs`)
  - `GET /health` (health checks: `src/nirmata.Api/Program.cs`)
  - `GET /v1/projects` etc. (controller: `src/nirmata.Api/Controllers/V1/ProjectController.cs`)
  - `GET /swagger` (Swagger UI)
- Daemon/engine API (`src/nirmata.Windows.Service.Api`):
  - `GET /` only (current placeholder)

Planned routes (documented in frontend hooks, not yet implemented):

- Daemon/engine API:
  - `GET /api/v1/health`
  - `PUT /api/v1/service/host-profile`
  - `POST /api/v1/commands`

Practical scan method (so you don’t guess):

- Search for hook usage:
  - `useWorkspace(`, `useWorkspaces(`, `useTasks(`, `useTaskPlans(`, `useRuns(`, `useIssues(`,
    `useHostConsole(`, `useDiagnostics(`, `useAosCommand(`, `useEngineConnection(`
- Search for direct network calls:
  - `fetch(` (currently only in `WorkspaceContext.tsx`)

Output of this step:

- A list of endpoints grouped by feature area (Plan/Spec, Runs/Evidence, Verification/UAT, Host/Daemon, Chat, etc.)
- For each endpoint: request/response shape (can be derived from the mock types in `mock*.ts`)

### 2) Backend endpoint creation + Swagger (HTTP API) [x]

Primary HTTP API project (ASP.NET Core):

- `src/nirmata.Api/Program.cs`
  - Swagger/OpenAPI is already enabled:
    - `builder.Services.AddSwaggerGen();`
    - `app.UseSwagger();` / `app.UseSwaggerUI(...)`
  - Health endpoints:
    - `app.MapHealthChecks("/health", ...)` (framework health checks)

Controllers (add new endpoints here unless you deliberately choose minimal APIs):

- `src/nirmata.Api/Controllers/HealthController.cs`
  - Route: `GET /api/health` (detailed health with DB check)
- `src/nirmata.Api/Controllers/V1/ProjectController.cs`
  - Route base: `/v1/projects`
  - Existing endpoints:
    - `GET /v1/projects`
    - `GET /v1/projects/search/{query}`
    - `GET /v1/projects/{projectId}`
    - `POST /v1/projects`

Where the business logic and data access lives (follow this chain when adding endpoints):

- Services registration (composition root):
  - `src/nirmata.Services/Composition/ServiceCollectionExtensions.cs`
- Service interfaces/implementations:
  - `src/nirmata.Services/Interfaces/*.cs`
  - `src/nirmata.Services/Implementations/*.cs`
  - Example: `src/nirmata.Services/Implementations/ProjectService.cs`
- Repository interfaces/implementations:
  - `src/nirmata.Data/Repositories/*.cs`
  - Example: `src/nirmata.Data/Repositories/ProjectRepository.cs`
- EF Core DbContext:
  - `src/nirmata.Data/Context/nirmataDbContext.cs`

DTOs / requests / validation:

- `src/nirmata.Data.Dto/Models/**`
- `src/nirmata.Data.Dto/Requests/**`
- `src/nirmata.Data.Dto/Validators/**`

Swagger verification:

- Run the API and open Swagger UI:
  - `GET /swagger`

Note: `src/nirmata.Api/nirmata.Api.http` currently points at a placeholder `/weatherforecast/` route. Update that file when real endpoints are confirmed.

### 3) Placeholder removal (wire UI to real endpoints without refactoring pages) [x]

Rule: do NOT fetch inside pages/components.

- Replace mock returns inside:
  - `nirmata.frontend/src/app/hooks/useAosData.ts`
- Keep pages/components unchanged (they already depend on hooks).

Concrete places where placeholders exist today:

- All `mock*` imports in `useAosData.ts` (top of file)
- The virtual filesystem is currently generated from mocks:
  - `nirmata.frontend/src/app/data/mockFileSystem.ts`
  - Consumers call `useFileSystem()` from `useAosData.ts`

Suggested wiring pattern (per endpoint):

- Add a typed fetch helper in `useAosData.ts` (or a small `api/` module under `nirmata.frontend/src/app/`)
- Update the corresponding hook to:
  - expose `{ data, isLoading, error }` (or whatever shape you standardize on)
  - keep the existing return keys stable so the UI doesn’t change

## Phase 2: Backend Integration & Service Setup (PAUSED)
Status: Paused

Blocked on / waiting for further instructions on:

### 4) Permanent Windows Service + Service API + endpoint routing map (API vs Windows Service API) [x]

There are two backend targets. The plan is to stand up the **real** Windows Service (`nirmata.Windows.Service`) and its **local daemon API** (`nirmata.Windows.Service.Api`) so the frontend always talks to a real process.

**Two backend targets:**

- Domain data API (ASP.NET Core, Swagger)
  - `src/nirmata.Api/Program.cs`
  - Owns: projects, workspaces, phases, tasks, runs, issues, spec artifacts
- Daemon/engine API (Windows Service local API)
  - `src/nirmata.Windows.Service.Api/Program.cs`
  - Owns: service start/stop/restart, health poll, host profile, engine connection
  - Service worker entry: `src/nirmata.Windows.Service/Program.cs` (`class Worker`)

**Routing rule:**
- “daemon/engine/service host” features (Settings Engine tab, Host Console, `WorkspaceContext` health poll) → `nirmata.Windows.Service.Api`
- “domain data” features (projects, workspaces, phases, tasks, runs, issues) → `nirmata.Api`

**Permanent daemon plan (replace MSW):**

1. Make `nirmata.Windows.Service.Api` the authoritative daemon surface and ensure it exposes the endpoints the frontend needs.
2. Make daemon API dev-friendly:
   - Run as a normal console-hosted ASP.NET Core app during development
   - Support production hosting alongside the Windows Service (either in-proc or as a companion process)
3. Add minimal service lifecycle endpoints (even if initially stubbed):
   - `GET /api/v1/health`
   - `GET /api/v1/service` (status)
   - `PUT /api/v1/service/host-profile`
   - `POST /api/v1/service/start`, `POST /api/v1/service/stop`, `POST /api/v1/service/restart`
4. Decide and document a single base URL for the daemon API used by the frontend (env-driven; default `http://localhost:9000`).
5. Ensure `nirmata.Windows.Service.Api` supports CORS for the frontend dev origin.
6. Replace any remaining daemon-side frontend mocks by wiring hooks to these daemon endpoints.

### 5) Console app vs Windows Service (debuggability + production shape) [x]


**What exists today (entrypoints):**

- `nirmata.Api` (`src/nirmata.Api/Program.cs`)
  - ASP.NET Core WebApplication (controllers, Swagger, EF Core SQLite, health checks).
  - Best for **domain data** (projects/workspaces/spec/runs/issues) and is already console-friendly for debugging.
- `nirmata.Windows.Service` (`src/nirmata.Windows.Service/Program.cs` + `Worker.cs`)
  - Generic Host running a `BackgroundService` loop.
  - As written, it is effectively a **console-hosted worker**. It does not currently opt into Windows Service hosting APIs (no `UseWindowsService`/service lifetime wiring yet).
- `nirmata.Windows.Service.Api` (`src/nirmata.Windows.Service.Api/Program.cs`)
  - Separate ASP.NET Core WebApplication intended to be the **daemon/engine/service-lifecycle surface**.
  - Listens on `DaemonApi:BaseUrl` (env var `DaemonApi__BaseUrl`, default `http://localhost:9000`).
  - CORS is configured via `Cors:AllowedOrigins` (dev default permits Vite origin).

**Frontend routing (single source of truth):**

- Daemon API base URL: `VITE_DAEMON_URL` (default `http://localhost:9000`).
- Domain API base URL: `VITE_DOMAIN_URL`.
- Keep the “routing rule” strict:
  - daemon/host/service lifecycle + engine commands → `nirmata.Windows.Service.Api`
  - workspace/domain data → `nirmata.Api`

**Dev workflow (debuggable by default):**

- Run `nirmata.Api` as a normal console process and debug via F5/attach.
- Run `nirmata.Windows.Service.Api` as a normal console process and debug via F5/attach.
- Run `nirmata.Windows.Service` as a normal console process and debug via F5/attach.
- Frontend points at these processes via `VITE_DAEMON_URL` / `VITE_DOMAIN_URL`.

**Production shape options (what we need to decide and document):**

1) **Companion process (recommended default until proven otherwise)**
   - Windows Service runs the long-lived worker/engine.
   - Daemon API (`nirmata.Windows.Service.Api`) runs as a separate process (or is supervised/launched by the service).
   - Pros:
     - Clean separation between API surface and worker engine.
     - Failure isolation (API crash doesn’t necessarily kill the engine, and vice versa).
     - Console-hosted dev experience matches production bits closely.
   - Cons:
     - Two processes to deploy/configure.
     - Need a clear ownership model for start/stop ordering and port binding.

2) **In-proc API hosted inside the Windows Service process**
   - Service host also starts Kestrel (daemon API) in the same process.
   - Pros:
     - Single process to install/deploy.
     - Simple localhost IPC story.
   - Cons:
     - Debuggability tends to be worse once running as a real service.
     - Any fatal API-level failure can take down the worker engine.
     - Requires careful shutdown and logging integration with the Service Control Manager.

**Where frontend-called endpoints belong:**

- Anything the frontend calls to control/observe the *host/service/engine* goes in `nirmata.Windows.Service.Api`.
- `nirmata.Windows.Service` should remain the “engine host” (background execution) and should not need to expose HTTP directly unless we explicitly choose the in-proc option.

---

## Phase 3: Workspace Registry & Domain Data Foundation
**Goal:** Stand up `nirmata.Api` with workspace CRUD and the spec/state read layer so `WorkspaceLauncherPage`, `WorkspaceDashboard`, and the file browser have real data.

### Backend
- [x] 3.1 `IWorkspaceService` + `WorkspaceService` 
  - Register/deregister workspace paths
  - Resolve `.aos/` presence, surface `WorkspaceSummary.status` 
  - Persist workspace registry (SQLite via `nirmata.Data`)
- [x] 3.2 `WorkspacesController` — `GET /v1/workspaces`, `POST`, `PUT`, `DELETE` 
- [x] 3.3 `IProjectService` — complete all items from §2 in your context (2.1–2.3)
- [x] 3.4 `ISpecService` (read-only pass 1)
  - `GET /v1/workspaces/:wsId/spec/milestones` 
  - `GET /v1/workspaces/:wsId/spec/phases` 
  - `GET /v1/workspaces/:wsId/spec/tasks` 
  - `GET /v1/workspaces/:wsId/spec/project` 
- [x] 3.5 `IFileSystemService` 
  - `GET /v1/workspaces/:wsId/files/*` — directory tree + raw file content
  - Gates: only allow paths within the registered workspace root

### Frontend
- [x] 3.6 Replace `useWorkspace` / `useWorkspaces` mock implementations with real `fetch` calls
- [x] 3.7 Replace `usePhases`, `useMilestones`, `useTasks`, `useProjectSpec` mock impls with real calls
- [x] 3.8 Replace `useFileSystem` mock with real calls (needed for `WorkspacePathPage`, `PlanPage`, `CodebasePage`)

### Verification
- All workspace list, dashboard, and plan pages render real `.aos/` data
- File browser navigates real workspace file tree
- `npm run typecheck && npm run build` pass

---

## Phase 4: Daemon API Completion
**Goal:** Finish the Windows Service API surface (health, service, commands, runs, logs, host-profile) and wire it to the frontend.

> This is the continuation of the paused Phase 2 items 4 & 5.

### Backend — `nirmata.Windows.Service.Api`
- [x] 4.1 `HealthController` — `GET /api/v1/health` returning `{ ok, version, uptimeMs }`
- [x] 4.2 `ServiceController` — `GET /api/v1/service` (status + surfaces), `PUT /api/v1/service/host-profile`
- [x] 4.3 `CommandsController` — `POST /api/v1/commands` (argv → `{ ok, output }`)
- [x] 4.4 `RunsController` (engine-level) — `GET /api/v1/runs`
- [x] 4.5 `LogsController` — `GET /api/v1/logs` (polling or SSE)
- [x] 4.6 `DiagnosticsController` — `GET /api/v1/diagnostics`, `DELETE` locks + cache endpoints

### Frontend
- [x] 4.8 Replace `useHostConsole` mock with real fetch against `VITE_DAEMON_URL`
- [x] 4.9 Replace `useAosCommand` stub with real `POST /api/v1/commands`
- [x] 4.10 Replace `useEngineConnection` test/save stubs with real health ping + profile PUT
- [x] 4.11 Replace `useDiagnostics` mock with real diagnostics endpoint
- [x] 4.12 `WorkspaceContext` health poll already targets real endpoint — remove `// no credentials` stub comment and confirm it lines up with `HealthController` response shape

### Verification
- With daemon API running, daemon routes return real data; health indicator goes green
- `useAosCommand` executes `aos status` and returns real output

---

## Phase 5: State, Continuity & Runs Integration
**Goal:** Wire `ContinuityPage`, `RunsPage`, and checkpoints to real AOS workspace state.

### Backend — `nirmata.Api` 
- [x] 5.1 `IStateService` + `StateService` 
  - Read `.aos/state/state.json` → `ContinuityState` 
  - Read `.aos/state/handoff.json` → `HandoffSnapshot | null` 
  - Tail `.aos/state/events.ndjson` → `Event[]` 
- [x] 5.2 `IEvidenceService` + `EvidenceService` 
  - List `.aos/evidence/runs/**` → `Run[]` 
  - Read single run folder → `Run` with artifacts + logs
- [x] 5.3 `StateController` — `GET /v1/workspaces/:wsId/state`, `/handoff`, `/events` 
- [x] 5.4 `CheckpointsController` — `GET /v1/workspaces/:wsId/checkpoints` 
- [x] 5.5 `RunsController` (domain) — `GET /v1/workspaces/:wsId/runs[?taskId=]`, `/:runId` 
- [x] 5.6 `ContextPacksController` — `GET /v1/workspaces/:wsId/state/packs` 

### Frontend
- [x] 5.7 Replace `useContinuityState` mock with real state + events + handoff endpoints
- [x] 5.8 Replace `useCheckpoints` mock with real checkpoints endpoint
- [x] 5.9 Replace `useRuns` mock with real runs endpoint

### Verification
- `ContinuityPage` shows real cursor, events, handoff from `.aos/state/` 
- `RunsPage` shows real run history from `.aos/evidence/runs/` 
- Pause/resume flow reflects actual `handoff.json` presence

---

## Phase 6: Issues, UAT & Verification Integration
**Goal:** Wire `VerificationPage` and `FixPage` to real issue and UAT data.

### Backend — `nirmata.Api` 
- [x] 6.1 `IIssueService` + `IssueService` 
  - CRUD for `.aos/spec/issues/**` 
  - Filter by status, severity, phaseId, taskId
- [x] 6.2 `IUatService` + `UatService` 
  - Read UAT records from `.aos/spec/uat/**` 
  - Derive pass/fail state per task/phase
- [x] 6.3 `IssuesController` — full CRUD + status update
- [x] 6.4 `UatController` — `GET /v1/workspaces/:wsId/uat` 

### Frontend
- [x] 6.5 Replace `useIssues` mock with real issues endpoint
- [x] 6.6 Replace `useVerificationState` with real-derived data from issues + tasks + runs endpoints
- [x] 6.7 Wire issue creation from `VerificationPage` → `POST /v1/workspaces/:wsId/issues` 
- [x] 6.8 Register `FixPage` in router (currently unregistered — gap from `transition.md`)

### Verification
- Open issues appear in `VerificationPage` with correct severity/status
- Creating an issue from UAT failure persists to `.aos/spec/issues/` 
- `FixPage` loads and can display fix tasks derived from open issues

---

## Phase 7: Codebase Intelligence & Orchestrator Gate Integration
**Goal:** Wire `CodebasePage` and the `WorkspaceDashboard` orchestrator gate to real data.

### Backend — `nirmata.Api` 
- [x] 7.1 `ICodebaseService` + `CodebaseService` 
  - Read `.aos/codebase/*.json` artifacts
  - Surface language breakdown and stack from `map.json` / `stack.json` 
- [x] 7.2 `IOrchestratorGateService` + `OrchestratorGateService` 
  - Derive `NextTaskGate` from state cursor + task plan + evidence
  - Expose gate checks (dependency, UAT, evidence)
- [x] 7.3 `CodebaseController` — `GET /v1/workspaces/:wsId/codebase`, `/:artifactId` 
- [x] 7.4 `OrchestratorController` — `GET /v1/workspaces/:wsId/orchestrator/gate`, `/timeline` 

### Frontend
- [x] 7.5 Replace `useCodebaseIntel` mock with real codebase endpoint
- [x] 7.6 Replace `useOrchestratorState` mock with real gate endpoint
- [x] 7.7 Fix orchestrator gating derivation logic (currently broken — noted gap in `transition.md`)

### Verification
- `CodebasePage` reflects real `.aos/codebase/` artifacts with correct status (ready/stale/missing)
- Gate on `WorkspaceDashboard` shows the real next task with correct pass/fail checks
- Gate derivation logic produces the right `runnable` flag based on actual state + evidence

---

## Phase 8: Chat / Command Interface
**Goal:** Implement the `ChatPage` — currently a minimal stub — as a real AOS command interface.

### Backend
- [x] 8.1 `IChatService` or extend `ICommandService` 
  - Accept freeform text → classify → dispatch `aos` command via `CommandsController` 
  - Return structured `OrchestratorMessage` (role, content, gate, artifacts, timeline, nextCommand)
  - Streaming optional (SSE or chunked response)
- [x] 8.2 `ChatController` — `POST /v1/workspaces/:wsId/chat` (or reuse `CommandsController`)

### Frontend
- [x] 8.3 Implement `ChatPage` beyond the current stub
  - Message thread with `role` rendering (user / assistant / system / result)
  - Timeline and artifact change display per `OrchestratorMessage` 
  - Command suggestion autocomplete from `mockCommandSuggestions` 
  - Quick action buttons from `mockQuickActions` 
- [x] 8.4 Replace `useChatMessages` mock with real streaming/polling endpoint

### Verification
- Chat page sends an `aos status` command and receives a real engine response
- Gate state + timeline steps update in the thread after a command runs
- TypeScript types align between `OrchestratorMessage` (frontend) and API response shape

---

## Phase 9: Debug, Stabilization & Launch Readiness
**Goal:** Get the app into a clean, low-noise state by fixing init-time failures, CORS/config mismatches, and the remaining endpoint/console errors.

### Backend
- [x] 9.1 Fix daemon CORS configuration for the frontend dev origin
  - Ensure `http://localhost:5173` is allowed for `nirmata.Windows.Service.Api`
  - Verify preflight handling for `GET /api/v1/health` and `POST /api/v1/commands`
- [x] 9.2 Validate daemon and domain startup URLs and port bindings
  - Confirm the frontend points at the correct daemon and domain base URLs
  - Remove any mismatched defaults that cause cross-origin or 500/404 noise during init
- [x] 9.3 Harden workspace endpoints during bootstrap
  - Make workspace lookup/registration errors explicit instead of surfacing as repeated console retries
  - Ensure `GET /v1/workspaces/:id` uses the same identifier shape the frontend supplies

### Frontend
- [x] 9.4 Stabilize `WorkspaceContext` initialization
  - Prevent repeated health polling noise when the daemon is unreachable or misconfigured
  - Surface a single actionable connection state instead of flooding the console
- [x] 9.5 Audit startup fetches and retry loops
  - Verify init flows do not call invalid workspace URLs such as non-guid IDs against GUID-only endpoints
  - Reduce duplicate requests for missing resources like favicon and bootstrap data
- [x] 9.6 Add clearer diagnostics for failed init state
  - Show the failing endpoint, status, and suggested fix in the UI/dev console
  - Distinguish CORS failures, 404s, and backend 500s in the error handling path

### Verification
- Browser console is clean at startup except for expected 404s during truly missing optional assets
- `GET /api/v1/health` succeeds from `http://localhost:5173` without CORS errors
- Workspace init completes without repeated 500/404 retries
- `nirmata.frontend` and both backend services boot together with stable dev defaults


## Phase 10: EF Core & SQLite Database Setup
**Goal:** Finish validating the EF Core + SQLite database path, confirm migrations are authoritative, and make local database setup repeatable for fresh clones and first boots.

### Database & Migrations
- [x] 10.1 Verify the EF Core migration history is complete and authoritative
  - Confirm the existing migration chain in `src/nirmata.Data/Migrations/` matches the current model
  - Decide whether any baseline or cleanup migration is needed before adding new schema work
- [x] 10.2 Validate the SQLite connection string, database file location, and bootstrap path
  - Confirm the runtime connection string points at the intended local database file
  - Make sure the design-time factory and startup configuration resolve the same database path
  - Verify the `sqllitedb/` directory exists or is created before first boot so SQLite can create/open the database file
- [x] 10.3 Standardize local EF tooling for developers
  - Document the exact `dotnet ef` commands needed for add/update/script workflows
  - Verify the project can create migrations from `nirmata.Data` with either the documented working directory or explicit `--project` / `--startup-project` flags
- [x] 10.4 Confirm database initialization on application startup
  - Ensure `Database.MigrateAsync()` runs safely on first boot and after schema changes
  - Verify the app starts cleanly when the SQLite file does not yet exist or when the schema must be upgraded
- [x] 10.5 Review seed data, model snapshots, and local SQLite artifacts
  - Confirm `HasData()` entries still match the intended baseline data
  - Check the snapshot and generated migrations stay in sync with `nirmataDbContext`
  - Decide how the local `.db`, `-wal`, and `-shm` files are handled in source control and clean-room setup

### Verification
- A fresh clone can create or update the SQLite database using documented EF commands
- Existing migrations apply cleanly with no manual schema steps
- Startup creates the database file and schema automatically on first run, even when `sqllitedb/` is missing
- Future model changes have a clear migration workflow, rollback path, and local artifact policy


