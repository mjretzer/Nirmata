## Context

Phase 7 is the point where the UI stops relying on placeholder codebase/orchestrator data and starts reflecting actual workspace state. The backend already exposes workspace-scoped endpoints for spec, state, runs, UAT, and checkpoints, so this change fits the same pattern: derive everything from the registered workspace root and keep the API contract workspace-scoped.

The current frontend hooks still assume broad codebase/orchestrator snapshots, and the dashboard gate logic contains local fallback derivation. That makes the next-step card and Codebase page vulnerable to stale or inconsistent state.

## Goals / Non-Goals

**Goals:**
- Serve workspace-scoped codebase intelligence from `.aos/codebase/` artifacts.
- Serve workspace-scoped orchestrator gate and timeline data from authoritative backend logic.
- Preserve the existing frontend hook shapes so page-level rendering changes stay small.
- Make gate derivation deterministic from workspace cursor, task plan, UAT, and evidence.

**Non-Goals:**
- Reworking the underlying `.aos` artifact formats.
- Changing the user-facing layout of `CodebasePage` or `WorkspaceDashboard` beyond what is needed to consume real data.
- Implementing a new orchestration engine or changing task execution semantics outside gate computation.

## Decisions

- **Use workspace-scoped endpoints in `nirmata.Api`**
  - Rationale: the rest of the domain API already scopes reads by workspace, and the new codebase/orchestrator data depends on workspace root access.
  - Alternatives considered: keep `/api/v1/codebase/intel` and `/api/v1/orchestrator/state` as global endpoints, or keep the current daemon placeholder endpoints. Those approaches do not model workspace ownership cleanly and make multi-workspace behavior harder to reason about.

- **Keep the frontend hook return shapes stable**
  - Rationale: `CodebasePage` and `WorkspaceDashboard` already consume `useCodebaseIntel()` and `useOrchestratorState()` shapes. Preserving those shapes limits churn and avoids a second UI refactor.
  - Alternatives considered: replace page consumers directly with raw API calls. That would spread network concerns into components and increase coupling.

- **Centralize gate derivation in a dedicated service**
  - Rationale: `runnable` must be derived consistently from dependencies, UAT, and evidence. A single service prevents the dashboard and any future API consumers from making different decisions.
  - Alternatives considered: derive gate state in the frontend using multiple hooks. That would duplicate business rules and reintroduce the broken local fallback pattern.

- **Treat artifact freshness as a backend concern**
  - Rationale: readiness should come from validated artifact presence and manifest hashes, not ad hoc UI heuristics.
  - Alternatives considered: infer status only from file existence. That would hide stale content and misreport readiness.

## Risks / Trade-offs

- **Large workspace scans may be slower** → Cache parsed artifact summaries where possible and read only the required JSON files for each request.
- **Artifact manifests may be incomplete or absent** → Return `stale`/`missing` statuses instead of failing the entire endpoint.
- **Gate derivation edge cases may surface in workspaces with partial data** → Use explicit blocking checks and fail closed when evidence or dependency data is missing.
- **Frontend/backend contract drift** → Keep the existing hook DTO mapping layer and add focused tests around the response adapters.

## Migration Plan

1. Add the backend services and controller endpoints behind the existing workspace API pattern.
2. Register the new services in DI and verify responses against existing `.aos` artifact shapes.
3. Update the frontend API client to call the new workspace-scoped endpoints.
4. Swap the hooks to the new responses while keeping their outward-facing shapes stable.
5. Update dashboard rendering to rely on the real gate response and remove local derivation fallbacks.

Rollback is straightforward: revert the client route updates first, then revert the new endpoint/service wiring if needed.

## Open Questions

- Should codebase artifact status validation rely strictly on `hash-manifest.json`, or should it also tolerate partial packs without a manifest?
- Should the orchestrator gate include only current task checks, or also expose a compact summary of downstream blockers?
- Should timeline ordering come from the current state cursor, the task plan order, or a separate orchestration snapshot file if one is added later?
