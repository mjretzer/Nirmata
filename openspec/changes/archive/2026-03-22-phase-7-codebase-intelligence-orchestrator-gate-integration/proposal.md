## Why

Phase 7 still relies on placeholder orchestrator and codebase responses, so the dashboard cannot reliably show the next runnable task or the current codebase artifact state. This change wires the UI to real workspace-scoped data from `.aos/codebase/` and the workspace’s task/evidence state so the product reflects actual readiness instead of mock heuristics.

## What Changes

- Add workspace-scoped codebase intelligence services and endpoints that read `.aos/codebase/*.json` and expose artifact readiness, language breakdown, and stack metadata.
- Add orchestrator gate services and endpoints that derive the next task gate from the workspace cursor, task plan, and evidence, including dependency, UAT, and evidence checks.
- Replace frontend mock hooks with API-backed codebase and orchestrator data while preserving the existing outward-facing hook shapes used by pages.
- Fix the workspace dashboard gate derivation so the next-step card uses real state and evidence instead of local fallback logic.

## Capabilities

### New Capabilities
- `codebase-intelligence`: Workspace-scoped codebase artifact inventory plus language and stack intelligence derived from `.aos/codebase/`.
- `orchestrator-gate`: Workspace-scoped gate computation, pass/fail checks, and timeline snapshot for the next task.

### Modified Capabilities
- `api`: Extend the API contract with workspace-scoped codebase and orchestrator endpoints and the richer DTOs they return.

## Impact

Backend controllers and services, frontend AOS data hooks, the `CodebasePage`, the `WorkspaceDashboard`, and verification coverage for artifact status and gate derivation are all affected.
