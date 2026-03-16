## Context
The engine’s evidence layer (`.aos/evidence/**`) is the canonical “provable truth” record for what happened during a run. Today, the run lifecycle already emits deterministic JSON artifacts and scaffolds run folders. PH-ENG-0006 extends that contract to add:
- a per-run command log (`runs/<run-id>/commands.json`)
- a per-run summary (`runs/<run-id>/summary.json`)
- a run artifacts bucket (`runs/<run-id>/artifacts/**`)
- a task-evidence “latest pointer” (`task-evidence/<task-id>/latest.json`)

The roadmap expects the run folder layout to follow a consistent, human-browsable structure.

## Goals / Non-Goals
### Goals
- Define a **restructured canonical** run evidence layout aligned to PH-ENG-0006.
- Define how legacy run layouts are handled during migration/transition.
- Define `task-evidence/<task-id>/latest.json` as an atomic, schema-valid pointer with commit hash and diffstat slots.
- Keep deterministic JSON semantics aligned with `aos-deterministic-json-serialization`.

### Non-Goals
- Implement the engine changes (this document supports the proposal stage).
- Define higher-level execution semantics (planning, execution, verification loops) beyond evidence outputs.
- Require git to be available in all environments (git-derived fields may be null/unknown when not available).

## Decisions
### Decision: Canonical run evidence layout is the PH-ENG-0006 “tree”
Canonical run folder structure:
- `.aos/evidence/runs/<run-id>/commands.json`
- `.aos/evidence/runs/<run-id>/summary.json`
- `.aos/evidence/runs/<run-id>/logs/`
  - `.aos/evidence/runs/<run-id>/logs/tool.log`
  - (engine may also emit structured logs under subfolders, e.g. `logs/calls/**`)
- `.aos/evidence/runs/<run-id>/artifacts/`
  - `.aos/evidence/runs/<run-id>/artifacts/diff.patch` (when applicable)

### Decision: Global commands log remains canonical (compat + audit)
Keep `.aos/evidence/logs/commands.json` as the canonical append-only audit log.

Add `.aos/evidence/runs/<run-id>/commands.json` as a per-run view (either independently appended at runtime or derived during/after execution). This avoids breaking existing bootstrap/validation expectations while meeting the roadmap’s per-run discoverability.

### Decision: Existing run lifecycle JSON artifacts are preserved, but placed under `artifacts/` (restructure)
Legacy artifacts are retained but re-homed to align with “artifacts” as the bucket for produced files:
- `run.json`, `packet.json`, `result.json`, `manifest.json` are considered run artifacts.
- Preferred placement (new writes): `.aos/evidence/runs/<run-id>/artifacts/{run.json,packet.json,result.json,manifest.json}`.

`summary.json` links to these artifacts using contract paths.

### Decision: Transition policy supports legacy runs
During migration:
- New runs SHOULD be written using the restructured layout.
- Workspace validation SHOULD accept both:
  - legacy layout (`runs/<run-id>/{run.json,packet.json,result.json,manifest.json,logs/,outputs/}`), and
  - restructured layout (`runs/<run-id>/{commands.json,summary.json,logs/,artifacts/}`), with a documented preference for the new layout.

This avoids making historical evidence invalid while tooling migrates.

### Decision: Task-evidence latest pointer includes roadmap “slots”
Define `.aos/evidence/task-evidence/<task-id>/latest.json` as a durable pointer to “latest evidence for this task”, with minimal required slots:
- `schemaVersion` (int)
- `taskId` (TSK-######)
- `runId` (32 lower-hex)
- `gitCommit` (string or null; commit hash slot)
- `diffstat` (object slot; minimal fields below)

Proposed `diffstat` minimal shape:
- `filesChanged` (int)
- `insertions` (int)
- `deletions` (int)

Optionally:
- `updatedAtUtc` (string timestamp)
- `artifacts` (object with contract path pointers like `diffPatch`, `summary`, `commands`)

## Risks / Trade-offs
- **Contract churn**: moving existing `run.json/packet.json/...` impacts validators, repairers, and tests → mitigated by explicitly supporting a legacy layout during transition.
- **Duplication**: global commands log + per-run commands log could diverge → mitigated by defining global as canonical and per-run as a view, with validation ensuring per-run entries’ `runId` matches the run directory.

## Migration Plan
- Phase 1: Update engine writers to emit restructured layout for new runs; continue writing global `commands.json`.
- Phase 2: Update validators to accept both layouts and report the “preferred” layout for new runs.
- Phase 3: Optionally add a repair/migration tool to relocate legacy run artifacts into `artifacts/` and/or generate `summary.json` for older runs (best-effort).

## Open Questions
- Should `task-evidence/latest.json` be updated only by task execution flows, or can it also be updated by manual CLI commands?
- Should per-run `commands.json` be written during execution (streaming append) or materialized at finish from the global log?

