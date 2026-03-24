## 1. Backend: daemon configuration and bootstrap behavior

- [x] 1.1 Review the daemon startup configuration and confirm the development listen URL and environment-driven defaults.
- [x] 1.2 Update daemon CORS policy so `http://localhost:5173` can call `GET /api/v1/health` and `POST /api/v1/commands` without preflight failures.
- [x] 1.3 Verify the daemon and domain base URL / port bindings are distinct and aligned with the frontend dev configuration.
- [x] 1.4 Make workspace lookup and registration/bootstrap errors explicit so invalid or missing workspaces are returned once instead of driving retries.

## 2. Frontend: initialization and retry stabilization

- [x] 2.1 Stabilize `WorkspaceContext` initialization so health polling stops when the daemon is unreachable or misconfigured.
- [x] 2.2 Audit startup fetches so invalid workspace identifiers are not sent to GUID-only workspace endpoints.
- [x] 2.3 Reduce duplicate startup requests for optional assets and bootstrap data, including favicon and initial workspace fetches.
- [x] 2.4 Add a single actionable connection state that replaces repeated console spam during init failures.

## 3. Diagnostics and verification

- [x] 3.1 Add clearer diagnostics that include the failing endpoint, HTTP status, and a suggested fix for connection and bootstrap failures.
- [x] 3.2 Add or update backend tests for daemon CORS / preflight behavior and workspace bootstrap failure handling.
- [x] 3.3 Add or update frontend tests for startup state handling, retry suppression, and diagnostic rendering.
- [x] 3.4 Verify the browser console is quiet at startup except for expected missing optional asset 404s.
- [x] 3.5 Verify `GET /api/v1/health` succeeds from `http://localhost:5173` and workspace initialization completes without repeated retries.
