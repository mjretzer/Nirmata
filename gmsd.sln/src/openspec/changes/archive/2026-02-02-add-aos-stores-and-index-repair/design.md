## Context
Milestone E1 aims to make `.aos/*` strict and enforceable: canonical routing, deterministic IO, rebuildable stores, and auditable evidence.

The codebase already provides:
- deterministic artifact ID → contract path routing (`aos-path-routing`)
- canonical deterministic JSON writes with atomic + no-churn semantics (`aos-deterministic-json-serialization`)
- workspace bootstrap (`aos init`) that seeds `project.json`, `roadmap.json`, and baseline catalog indexes
- workspace validation (`aos validate workspace`) that validates a subset of required JSON artifacts
- run lifecycle scaffold with a deterministic run index (`aos-run-lifecycle`)

This change adds the missing store layer contracts and deterministic repair so the workspace is reconstructable from disk without hidden state.

## Goals / Non-Goals
- Goals:
  - Define strict, deterministic on-disk contracts for spec/state/evidence layers.
  - Ensure catalog indexes and run index are **rebuildable deterministically** from disk.
  - Provide a single repair entry-point: `aos repair indexes`.
  - Keep all `.aos/**` writes canonical (atomic, stable bytes, no churn).
- Non-Goals:
  - Full schema completeness for every artifact type (will evolve after contracts stabilize).
  - Multi-project workspaces (single-project invariants remain).

## Decisions
### Decision: Index documents remain “IDs only”
The existing `catalog-index.schema.json` defines:
- `schemaVersion: 1`
- `items: string[]`

Indexes will list **only artifact IDs** (not paths), because routing is the only allowed mechanism for resolving paths (`aos-path-routing`). This avoids duplicated truth and keeps indexes small and stable.

### Decision: Repair uses deterministic enumeration + sorting
Index repair will enumerate artifacts from contract locations on disk and write indexes deterministically:
- only known roots are scanned (e.g. `.aos/spec/milestones/*/milestone.json`)
- all discovered IDs are validated using the routing/ID parser
- IDs are sorted using **ordinal** string ordering before writing
- indexes are written using the canonical deterministic JSON writer (atomic + no-churn)

### Decision: State is split into snapshot + append-only events
State persists two complementary artifacts:
- `.aos/state/state.json` as the latest snapshot (cursor/status)
- `.aos/state/events.ndjson` as an append-only log of state transitions and notable actions

This enables resumability and auditability without requiring hidden runtime state.

### Decision: Evidence standardizes “what ran” and “what changed”
Evidence must be queryable and reconstructable from disk:
- command logs are recorded in a machine-readable evidence log
- runs include manifests that enumerate produced artifacts and hashes, enabling verification

## Risks / Trade-offs
- **Filesystem scanning vs determinism**: repair necessarily depends on disk state; we mitigate nondeterminism by sorting and restricting scan roots to canonical contract locations.
- **Contract strictness vs migration**: making new artifacts required can break older workspaces. Mitigation: provide repair commands and clear diagnostics; document minimal migration path.
- **Schema lag**: not adding schemas for every artifact type can reduce validation strength initially. Mitigation: validate JSON correctness and deterministic writing now; add schema coverage once structures stabilize.

## Migration Plan
- `aos init` defines the baseline, strict workspace scaffold (including required new artifacts).
- Existing repos with an older `.aos/` may require:
  - re-running `aos init` (expected to fail fast with actionable missing-path diagnostics), then
  - using `aos repair indexes` to rebuild missing/invalid indexes deterministically.

## Open Questions
- Should `events.ndjson` be strictly JSON-object-per-line only, or allow comments/blank lines? (proposal assumes blank lines are allowed but ignored during validation/repair.)
- Should command logs be NDJSON instead of JSON array for append-only behavior? (roadmap cites `commands.json`; proposal keeps JSON for now, but may switch if append-only requirements increase.)

