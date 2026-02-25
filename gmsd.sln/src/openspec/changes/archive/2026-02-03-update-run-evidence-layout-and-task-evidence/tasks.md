## 1. Specification updates
- [x] 1.1 Add delta spec for new capability `aos-task-evidence` (latest pointer contract + scenarios).
- [x] 1.2 Modify `aos-run-lifecycle` delta spec to require per-run `commands.json`, `summary.json`, `logs/`, `artifacts/` (transition notes included).
- [x] 1.3 Modify `aos-evidence-store` delta spec to define the per-run `commands.json` view and its relationship to the global commands log.
- [x] 1.4 Modify `aos-workspace-validation` delta spec to validate new evidence artifacts when present (and tolerate legacy runs during transition).

## 2. Engine implementation: restructured run evidence (PH-ENG-0006)
- [x] 2.1 Update run evidence scaffolding to create `runs/<run-id>/{logs/,artifacts/}` and new `runs/<run-id>/{commands.json,summary.json}`.
- [x] 2.2 Decide and implement placement of legacy run artifacts (`run.json`, `packet.json`, `result.json`, `manifest.json`, `outputs/`) under the restructured layout, preserving determinism.
- [x] 2.3 Implement deterministic `summary.json` writer (schemaVersion/runId/status/timestamps/exitCode + artifact pointers).
- [x] 2.4 Implement per-run `commands.json` writer/view (ensure entries’ `runId` matches run folder).
- [x] 2.5 Update any evidence/path helpers and routing call sites to avoid ad-hoc path building.

## 3. Engine implementation: task-evidence latest pointer
- [x] 3.1 Add `Gmsd.Aos/Engine/Evidence/TaskEvidence/**` writer for `.aos/evidence/task-evidence/<task-id>/latest.json`.
- [x] 3.2 Ensure atomic updates and deterministic JSON semantics (canonical ordering + no-churn).
- [x] 3.3 Populate required slots: `gitCommit` (nullable when git unavailable) and `diffstat` (with stable defaults when unknown).

## 4. Schemas + workspace validation
- [x] 4.1 Add JSON schema(s) for `runs/<run-id>/summary.json`, per-run `commands.json`, and `task-evidence/<task-id>/latest.json` (if/when schema-pack coverage is desired for these artifacts).
- [x] 4.2 Update workspace validation to validate the new artifacts when present and handle legacy run layouts during transition.

## 5. Tests
- [x] 5.1 Add tests that `aos run start` creates the restructured evidence tree.
- [x] 5.2 Add tests that `aos run finish` produces `summary.json` and keeps indexes deterministic.
- [x] 5.3 Add tests for atomic update semantics of `task-evidence/.../latest.json` (no partial writes; schema-valid).
- [x] 5.4 Add/adjust snapshot fixtures for the new contract paths and artifacts.

## 6. Validation
- [x] 6.1 `openspec validate update-run-evidence-layout-and-task-evidence --strict`
- [x] 6.2 Ensure CI still passes (unit tests + any OpenSpec checks).

