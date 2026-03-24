## 1. Daemon API Implementation
- [x] 1.1 Implement `GET /api/v1/health`
- [x] 1.2 Implement `PUT /api/v1/service/host-profile`
- [x] 1.3 Implement `POST /api/v1/commands`

## 2. Domain Data API Implementation (Read-Only)
- [x] 2.1 Implement `GET /api/v1/workspaces` and `GET /api/v1/workspaces/{workspaceId}`
- [x] 2.2 Implement `GET /api/v1/tasks` (with filters)
- [x] 2.3 Implement `GET /api/v1/task-plans`
- [x] 2.4 Implement `GET /api/v1/runs`
- [x] 2.5 Implement `GET /api/v1/issues`
- [x] 2.6 Implement `GET /api/v1/checkpoints`
- [x] 2.7 Implement `GET /api/v1/continuity` (State, Handoff, Events, Packs)
- [x] 2.8 Implement `GET /api/v1/filesystem` (Virtual FS)
- [x] 2.9 Implement `GET /api/v1/host/logs` and `GET /api/v1/host/surfaces`
- [x] 2.10 Implement `GET /api/v1/diagnostics` (Logs, Artifacts, Locks, Cache)
- [x] 2.11 Implement `GET /api/v1/codebase/intel`
- [x] 2.12 Implement `GET /api/v1/orchestrator/state`
- [x] 2.13 Implement `GET /api/v1/chat` (Messages, Suggestions)

## 3. Frontend Integration
- [x] 3.1 Create API client in frontend
    - [x] 3.1.1 Add `axios` or similar fetch utility to `nirmata.frontend`
    - [x] 3.1.2 Define `BaseApiClient` in `src/app/utils/apiClient.ts` with error handling
    - [x] 3.1.3 Implement `DaemonClient` for `/api/v1/health`, `/api/v1/commands`, etc.
    - [x] 3.1.4 Implement `DomainClient` for `/api/v1/workspaces`, `/api/v1/tasks`, etc.
- [ ] 3.2 Swap `useAosData.ts` implementations to use real API
    - [x] 3.2.1 Update `useWorkspace` and `useWorkspaces` to fetch from `DomainClient`
    - [x] 3.2.2 Update `useTasks`, `useTaskPlans`, `useRuns`, and `useIssues`
    - [x] 3.2.3 Update `useContinuityState`, `useFileSystem`, and `useProjectSpec`
    - [x] 3.2.4 Update `useHostConsole`, `useDiagnostics`, and `useCodebaseIntel`
    - [x] 3.2.5 Update `useOrchestratorState` and `useChatMessages`
    - [x] 3.2.6 Update command hooks: `useAosCommand`, `useEngineConnection`, `useWorkspaceInit`
- [x] 3.3 Verify Frontend Integration
    - [x] 3.3.1 Ensure loading states (`isLoading`, `isTesting`, etc.) are correctly handled
    - [x] 3.3.2 Add basic error boundaries or toast notifications for API failures
    - [x] 3.3.3 End-to-end verification with backend running locally
