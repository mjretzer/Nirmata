## 1. Daemon API backend completion

- [x] 1.1 Update `src/nirmata.Windows.Service.Api/Controllers/v1/HealthController.cs` so `GET /api/v1/health` returns `{ ok: true, version, uptimeMs }` and `uptimeMs` is derived from daemon process start, not request latency.
- [x] 1.2 Update `src/nirmata.Windows.Service.Api/Controllers/v1/ServiceController.cs` so `GET /api/v1/service` returns `{ ok: true, status, surfaces }`; `status` is the current service state and `surfaces` describes the daemon/host API surfaces the UI renders.
- [x] 1.3 Keep `PUT /api/v1/service/host-profile` on `ServiceController`; accept `{ hostName, workspacePath, metadata }` from the frontend and persist the latest profile in daemon runtime state.
- [x] 1.4 Replace `CommandsController` with `POST /api/v1/commands` accepting `{ argv: string[] }` and returning `{ ok: boolean, output: string }`; execute the argv command through the daemon backend and return the captured output.
- [x] 1.5 Add `GET /api/v1/runs` in `src/nirmata.Windows.Service.Api/Controllers/v1/RunsController.cs` for engine-level run summaries; support simple query filtering only if the runtime data source already exposes it.
- [x] 1.6 Add `GET /api/v1/logs` in a daemon controller for recent host logs; polling is sufficient for this phase, SSE is optional.
- [x] 1.7 Expand `src/nirmata.Windows.Service.Api/Controllers/v1/DiagnosticsController.cs` so `GET /api/v1/diagnostics` returns logs/artifacts/locks/cache and `DELETE /api/v1/diagnostics/locks` + `DELETE /api/v1/diagnostics/cache` clear stale entries.
- [x] 1.8 Register any daemon services/state providers needed by health, service status, command execution, runs, logs, and diagnostics in the daemon host composition root (`Program.cs` / DI setup).

## 2. Frontend daemon wiring

- [x] 2.1 Update `src/nirmata.frontend/src/app/hooks/useAosData.ts::useHostConsole()` so it reads from `daemonClient.getHostLogs()` and `daemonClient.getHostSurfaces()` against `VITE_DAEMON_URL`.
- [x] 2.2 Update `src/nirmata.frontend/src/app/hooks/useAosData.ts::useAosCommand()` so `execute(argv)` sends `POST /api/v1/commands` and resolves the backend `{ ok, output }` response.
- [x] 2.3 Update `src/nirmata.frontend/src/app/hooks/useAosData.ts::useEngineConnection()` so `test(baseUrl)` pings `GET /api/v1/health` and maps the daemon health payload, and `save(profile)` sends `PUT /api/v1/service/host-profile`.
- [x] 2.4 Update `src/nirmata.frontend/src/app/hooks/useAosData.ts::useDiagnostics()` so it consumes `GET /api/v1/diagnostics` and maps logs, artifacts, locks, and cache entries from the daemon response.
- [x] 2.5 Update `src/nirmata.frontend/src/app/context/WorkspaceContext.tsx` health polling so it still hits the daemon health endpoint, removes the `// no credentials / no cookies` stub comment, and matches the final `HealthController` response shape.
- [x] 2.6 Verify `src/nirmata.frontend/src/app/api/routing.ts` and `src/nirmata.frontend/src/app/utils/apiClient.ts` keep daemon features on `VITE_DAEMON_URL` and domain features on `VITE_DOMAIN_URL`; do not cross-route daemon calls to the domain client.

## 3. Validation and release checks

- [x] 3.1 Add or update backend tests for `GET /api/v1/health`, `GET /api/v1/service`, `PUT /api/v1/service/host-profile`, `POST /api/v1/commands`, `GET /api/v1/runs`, `GET /api/v1/logs`, and `GET`/`DELETE` diagnostics endpoints.
- [x] 3.2 Add or update frontend hook tests for `useHostConsole`, `useAosCommand`, `useEngineConnection`, `useDiagnostics`, and the `WorkspaceContext` health poll path.
- [x] 3.3 Smoke test the daemon API locally with the frontend dev server using `http://localhost:9000` or the configured `VITE_DAEMON_URL`; verify health turns green and command execution returns real output.
- [x] 3.4 Confirm the Phase 4 roadmap items in `ROADMAP.md` can be checked off only after the backend endpoints and frontend wiring return real data.
