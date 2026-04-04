## Context

The current Plan page (`nirmata.frontend/src/app/pages/PlanPage.tsx`) mixes canonical workspace-backed data with synthetic fallback JSON generation and local mutable UI task state in plan components. This diverges from documented AOS planning artifacts in `docs/architecture/schemas.md`, `docs/workflows/gating.md`, and `docs/agents/planning.md`, where task/phase plans are represented as persisted `.aos/spec/**` artifacts. The design must preserve current route structure while ensuring plan views are deterministic and grounded in real workspace data.

## Goals / Non-Goals

**Goals:**
- Make Plan page rendering artifact-backed for `.aos/spec` paths and eliminate synthetic/fabricated fallback content.
- Align root, phase, and artifact lenses with documented planning artifacts (`roadmap.json`, `phases/PH-*/phase.json`, `tasks/TSK-*/task.json`, `tasks/TSK-*/plan.json`).
- Standardize loading, empty, and missing-artifact behavior for plan routes.
- Keep plan task/phase displays sourced from canonical hooks/API responses to avoid local-only divergence.

**Non-Goals:**
- Redesigning Plan page visual style or information architecture beyond behavior corrections.
- Changing backend endpoint shapes or introducing new backend APIs.
- Implementing write workflows for task/phase editing in this change.

## No-Guessing Constraints

- Do not synthesize `.aos/spec` artifact JSON in the UI when data is unavailable.
- Do not infer missing artifact content from related lists; render explicit missing-artifact state instead.
- Do not introduce new backend endpoints or modify endpoint paths for this change.
- Do not change route contracts under `/ws/:workspaceId/files/.aos/spec`.
- Do not use local mutable copies as the source of truth for rendered milestone, phase, or task data.

## Decisions

### Decision 1: Treat `.aos/spec` as the source of truth for artifact views
- Choice: Resolve Plan page content from workspace file/spec reads and existing summary endpoints rather than generating fallback pseudo-artifacts.
- Rationale: Documentation defines these artifacts as intended truth and test/verification inputs. UI-generated pseudo content creates drift and hides missing backend data.
- Alternatives considered:
  - Keep synthetic fallback for convenience: rejected because it obscures missing artifacts and can mislead execution/verification workflows.
  - Replace with fully mocked fixtures in UI: rejected because fixtures are still non-canonical in workspace-scoped mode.

### Decision 2: Keep route semantics but make lens behavior explicit and deterministic
- Choice: Preserve route handling (`/ws/:workspaceId/files/.aos/spec`, phase directories, artifact file paths), with deterministic resolution rules per path class.
- Rationale: Existing navigation and command palette depend on this route shape; behavior should be corrected without breaking path contracts.
- Alternatives considered:
  - Introduce a separate route tree for plan lenses: rejected as unnecessary churn.

### Decision 3: Move plan component state to canonical data-first model
- Choice: Plan list and detail views should read from canonical hooks (`usePhases`, `useTasks`, `useTaskPlans`, `useFileSystem`) and avoid local mutated copies as primary source.
- Rationale: Local-only state edits are not reflected in persisted artifacts and create inconsistent results across navigation.
- Alternatives considered:
  - Continue local state and sync later: rejected due to ambiguity and stale view risk.

## Risks / Trade-offs

- Risk: Removing fallback content may expose existing data gaps as empty/error states in environments with incomplete `.aos/spec` trees. -> Mitigation: define explicit empty/missing-artifact states and ensure they include actionable path context.
- Risk: Some plan component interactions currently rely on local transient state for demos. -> Mitigation: scope this change to read-path correctness; follow up with dedicated write-path proposal if needed.
- Risk: Route/path matching regressions can break deep links. -> Mitigation: add focused frontend tests for root, phase, task, and plan artifact routes.

## Migration Plan

1. Update Plan page and plan components to remove synthetic artifact generation from rendering paths.
2. Normalize path-resolution behavior for root, phase directory, and file artifact views.
3. Update hooks/components to consume canonical data as primary source for displayed task/phase details.
4. Add/adjust tests for route and artifact resolution behavior.
5. Validate with frontend test run and manual spot-check navigation from command palette and sidebar.

## Open Questions

- None. This change intentionally defers quick-action UX additions and mandates missing-artifact rendering when required artifacts are absent.
