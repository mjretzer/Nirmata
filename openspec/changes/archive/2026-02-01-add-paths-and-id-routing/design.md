## Context
Milestone E1 (“AOS Core Workspace Contract”) requires `.aos/*` to be enforceable and machine-operable. A core prerequisite is deterministic routing: given an artifact ID, the engine must be able to resolve the one correct path for that artifact without scanning or guessing.

The codebase already contains localized routing/policy logic for:
- run evidence roots under `.aos/evidence/runs/**`
- execute-plan output scoping (safe relative path resolution under a root)

However, as soon as the CLI adds authoring commands for milestones/phases/tasks/issues/UAT (and later stores/index repair), routing logic will proliferate unless the project defines a single source of truth.

## Goals / Non-Goals
### Goals
- Define a deterministic mapping from supported artifact IDs to their canonical contract paths under `.aos/*`.
- Define ID parsing and validation rules so the router can fail fast with actionable errors.
- Ensure there is one “routing truth” that engine/CLI code must call into (eliminate ad-hoc path building).

### Non-Goals
- Implement CRUD stores and index repair (covered by later phases in the roadmap).
- Define all JSON schemas for every artifact type (this phase defines routing, not schema completeness).
- Migrate existing workspaces to new layouts (migration system is a later phase).

## Decisions
### Decision: Contract paths are returned (not physical paths)
The router’s primary output is a **contract path** like `.aos/spec/tasks/TSK-000001/task.json`. This keeps the routing rules stable and reviewable, and makes diagnostics match the spec language. Implementation can derive full filesystem paths by joining the repository root.

### Decision: Spec artifacts use folder-per-artifact layout
For spec objects that will grow attachments (plan/uat/links/research/assumptions), routing targets a folder-per-artifact layout:
- `.aos/spec/milestones/<MS-####>/milestone.json`
- `.aos/spec/phases/<PH-####>/phase.json` (+ optional siblings)
- `.aos/spec/tasks/<TSK-######>/task.json` (+ optional siblings)
- `.aos/spec/issues/<ISS-####>/issue.json`
- `.aos/spec/uat/<UAT-####>/uat.json`

### Decision: RUN IDs remain as current engine format (for now)
RUN IDs remain **32-char lower-hex GUIDs** (as already generated/validated by the engine today), and route under:
- `.aos/evidence/runs/<runId>/...`

The router SHOULD be designed to allow adding `RUN-*` formats later without changing callers, but GUID remains canonical in this change.

### Decision: ID validation is strict and deterministic
To avoid path ambiguity and to keep IDs folder-safe:
- MS IDs MUST match `MS-` + 4 digits
- PH IDs MUST match `PH-` + 4 digits
- TSK IDs MUST match `TSK-` + 6 digits
- ISS IDs MUST match `ISS-` + 4 digits
- UAT IDs MUST match `UAT-` + 4 digits
- RUN IDs MUST match the current engine format

## Risks / Trade-offs
- Introducing folder-per-artifact routing will diverge from the current minimal bootstrap artifacts (which only seed indexes). This is expected in E1 and will be handled incrementally by later phases (stores, indexes, schemas).
- If we later adopt a different RUN ID scheme, routing rules must remain backward-compatible (or migration must be introduced). Keeping GUID canonical now minimizes churn in existing engine behavior.

## Developer notes
- The routing source of truth is `Gmsd.Aos/Engine/Paths/AosPathRouter.cs`.
- Router outputs are **contract paths** (always using `/` separators), e.g. `.aos/spec/tasks/TSK-000001/task.json`.
- Engine/CLI code SHOULD derive filesystem paths via router helpers (e.g. `GetRunOutputsRootPath`) rather than hand-building `.aos/*` paths.

