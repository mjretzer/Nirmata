# Change: Update run evidence layout and add task-evidence pointers

## Why
PH-ENG-0006 requires “provable truth” artifacts that are easy to inspect, link, and validate without relying on chat history. Today, runs already emit deterministic evidence (`run.json`, `packet.json`, `result.json`, `manifest.json`), but the roadmap target introduces additional standard artifacts (`summary.json`, per-run `commands.json`, and an `artifacts/` bucket) and a task-scoped “latest evidence” pointer.

This change proposes a **restructured** run evidence layout aligned to the roadmap, plus a new task-evidence pointer contract under `.aos/evidence/task-evidence/`.

## What Changes
- Standardize the **per-run evidence tree** to include:
  - `.aos/evidence/runs/<run-id>/commands.json`
  - `.aos/evidence/runs/<run-id>/summary.json`
  - `.aos/evidence/runs/<run-id>/logs/` (including `tool.log`)
  - `.aos/evidence/runs/<run-id>/artifacts/` (including `diff.patch` when applicable)
- Introduce **task-evidence latest pointers**:
  - `.aos/evidence/task-evidence/<task-id>/latest.json` updated atomically and schema-valid.
  - Includes **commit hash slot** and **diffstat slot** (plus a run pointer so it’s actionable).
- Define migration/compatibility behavior for existing runs created with the legacy layout.

## Impact
- **BREAKING (workspace contract)**: the canonical on-disk run layout changes; tooling and validation may need to accept both layouts during transition.
- **Affected specs**:
  - `aos-run-lifecycle` (run folder contract + new `summary.json`)
  - `aos-evidence-store` (per-run commands view and run manifest/output placement)
  - `aos-workspace-validation` (validation of new artifacts and transition behavior)
  - new capability: `aos-task-evidence`
- **Affected code (implementation stage)**:
  - `Gmsd.Aos/Engine/Evidence/**` (run evidence writers and new task-evidence writer)
  - `Gmsd.Aos/Engine/Validation/**` (workspace validation rules)
  - `Gmsd.Aos/Resources/Schemas/**` (schemas for new artifacts)

