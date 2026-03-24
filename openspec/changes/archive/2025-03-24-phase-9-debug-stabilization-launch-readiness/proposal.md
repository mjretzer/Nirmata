## Why

Phase 9 is the stabilization pass for startup and initialization. The app still produces avoidable console noise when daemon and domain defaults do not match, CORS is too strict for local development, workspace bootstrap calls retry on invalid identifiers, and the frontend keeps polling or re-fetching after failures instead of surfacing one clear connection state. This change makes local startup predictable and low-noise.

## What Changes

- Update daemon CORS and dev defaults so `http://localhost:5173` can call `GET /api/v1/health` and `POST /api/v1/commands` without preflight failures.
- Align daemon and domain base URL and port configuration so the frontend targets the correct services during bootstrap.
- Make workspace lookup, registration, and bootstrap errors explicit so missing or invalid workspaces do not trigger repeated retry loops.
- Stabilize `WorkspaceContext` and related startup fetches so health polling, workspace lookup, favicon, and bootstrap requests do not flood the console.
- Add clearer diagnostic output that identifies the failing endpoint, status, and likely fix.

## Impact

Backend daemon configuration and workspace bootstrap handling, frontend startup state management, and verification coverage for local startup and CORS behavior are affected.
