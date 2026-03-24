## Context

Phase 9 is a stabilization pass after the core workspace, codebase, orchestrator, and chat flows already exist. The remaining problems are mostly initialization noise: mismatched daemon/domain defaults, CORS gaps in local development, repeated workspace bootstrap retries, and frontend polling that keeps firing when the daemon is unreachable or misconfigured. The goal is not to add new behavior; it is to make startup behavior explicit, quiet, and diagnosable.

## Goals / Non-Goals

**Goals:**
- Allow the frontend dev origin to reach the daemon API without CORS failures.
- Keep daemon and domain base URL responsibilities distinct and consistent with the existing routing boundary.
- Stop repeated health and workspace bootstrap retries from flooding the console.
- Surface a single actionable initialization state in the UI and dev console.
- Improve diagnostics so failed init states identify the endpoint, status, and likely fix.

**Non-Goals:**
- Reworking the workspace registry model or identifier format.
- Changing the behavior of the existing AOS command execution flow.
- Introducing a new networking stack or replacing the current HTTP clients.
- Redesigning the startup UI beyond the minimum needed to communicate failures clearly.

## Decisions

- **Keep daemon CORS explicit and development-focused**
  - Rationale: the local frontend needs to call the daemon during development, but production should stay constrained to the configured origins.
  - Alternatives considered: permissive wildcard CORS, or leaving the current restrictive defaults in place. Both would either weaken security or continue the startup failures.

- **Treat daemon base URL and domain base URL as separate configuration concerns**
  - Rationale: service lifecycle, health, and command endpoints belong to the daemon, while workspace and domain data belong to the domain API.
  - Alternatives considered: merge them into one generic base URL. That would blur the routing boundary and make misconfiguration harder to detect.

- **Gate polling on connection state instead of retrying blindly**
  - Rationale: repeated retries are noisy and obscure the actual failure. A single visible disconnected or misconfigured state is easier to act on.
  - Alternatives considered: keep aggressive retry loops and rely on console errors. That preserves noise and does not help the operator.

- **Normalize workspace identifiers before startup requests**
  - Rationale: the frontend should only call endpoints with the identifier shape the backend expects, and mismatches should be handled once at the boundary.
  - Alternatives considered: let each caller guess the correct identifier format. That would spread bootstrap bugs across multiple hooks.

- **Surface structured diagnostics for failed init paths**
  - Rationale: endpoint, status, and suggested fix are enough to debug most startup issues without opening the network tab.
  - Alternatives considered: generic failure banners or raw console output. Those are less actionable.

## Risks / Trade-offs

- **Stricter failure handling may reduce transient auto-recovery** → Keep state transitions explicit but allow a controlled retry path after configuration changes.
- **CORS and base URL changes may require local environment updates** → Document the expected dev defaults and keep the frontend/daemon configuration aligned.
- **Diagnostics could leak sensitive details if over-verbose** → Limit messages to endpoint shape, status, and high-level remediation.
- **Startup fetch suppression may hide legitimate optional asset requests** → Allow expected missing-asset 404s while suppressing duplicate retries for the same failure.

## Migration Plan

1. Align daemon configuration defaults and CORS policy with the frontend development origin.
2. Verify the frontend uses the correct daemon and domain base URLs for each class of request.
3. Update `WorkspaceContext` and startup fetch layers to stop repeated retries on unreachable or misconfigured services.
4. Add explicit diagnostics for connection, CORS, and workspace bootstrap failures.
5. Verify startup behavior in the browser and in automated tests before considering the change complete.
