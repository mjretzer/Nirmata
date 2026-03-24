# Verification & Fix Agents

Source: Validation__Debug.pdf (Section 4)

---

## UAT Verifier Agent

### Responsibilities

- Act as the **formal acceptance gate**: executed work is not "done" until UAT is recorded as pass or fails with scoped issues.
- Drive phase/plan verification (`verify-work <PH|PLAN>`), aligned to intended outcomes from spec/roadmap/state/plan artifacts.
- Convert qualitative feedback ("feels wrong / broke") into concrete, scoped, reproducible issues suitable for fix planning.
- Prevent hidden defect accumulation by requiring explicit pass/fail outcomes and persisting evidence.

### Step Format (single run)

1. Receive `verify-work <PH-#### | PLAN | TSK-…>` intent (CLI equivalent) and load `.aos/spec/project.json`, `.aos/spec/roadmap.json`, `.aos/state/state.json`, and the relevant task/phase plans under `.aos/spec/tasks/**`.
2. Build a UAT checklist from the acceptance criteria already defined in task plans (and phase outcomes) and open a verification run record in `.aos/evidence/runs/RUN-*/`.
3. Guide UAT: prompt the operator through validating each expected behavior and recording pass/fail observations with reproduction steps and environment notes.
4. Capture findings as structured issues: create/update `.aos/spec/issues/ISS-*.json` (severity, scope, repro, expected vs actual, impacted files/area) and persist UAT output under `.aos/spec/tasks/TSK-*/uat.json` or `.aos/spec/uat/UAT-*.json` (per schema convention).
5. Persist the verification result: append `uat.completed` event, update `.aos/state/state.json` cursor status to `verified-pass` or `verified-fail`, and attach evidence artifacts to the run.
6. Hand off to Orchestrator: if fail, route to `plan-fix` with the generated issues; if pass, advance to next phase/milestone planning/execution.

### Summary

UAT Verifier is the reality-check gate between "executed tasks" and "ready to proceed." It runs a structured verification aligned to the project and phase intent, records explicit pass/fail outcomes with evidence, and converts any gaps into scoped, reproducible issue artifacts that feed directly into fix planning. This prevents the workflow from drifting forward on unvalidated assumptions and keeps quality control deterministic and auditable.

---

## Fix Planner Agent

### Responsibilities

- Consume UAT findings produced by `verify-work` and convert them into an execution-ready, atomic fix plan.
- Keep fixes small (**max ~3 tasks per plan**) to preserve the "fresh subagent per task" execution model.
- Embed verification as first-class: every fix task must specify checks that directly re-run the failing UAT scenarios.
- Persist fix plans as canonical task plan artifacts so execution is deterministic and resumable.

### Step Format (single run)

1. Load the working set: `.aos/spec/project.json`, `.aos/spec/roadmap.json`, `.aos/state/state.json`, plus the most recent relevant task plans and narrative/evidence (latest RUN-*, task-evidence `latest.json`).
2. Ingest verification output deterministically by loading the persisted UAT artifact(s) (e.g., `.aos/spec/tasks/TSK-*/uat.json` or `.aos/spec/uat/UAT-*.json`) and the referenced issues in `.aos/spec/issues/ISS-*.json`.
3. Select an atomic fix scope: choose the smallest coherent subset of issues that can be fixed and verified quickly (target 2–3 fix tasks total).
4. Write the fix plan as new task plan artifacts: create/update `.aos/spec/tasks/TSK-*/plan.json` (or create dedicated fix tasks) with explicit file scope, precise actions, verification commands, and definition-of-done tied to the UAT failures.
5. Validate (`aos validate spec`), append a `fix.planned` event, and update cursor status in `.aos/state/state.json` to `"ready-to-execute-fix"`.
6. Hand off to Orchestrator with the next action: `execute-plan` (then re-run `verify-work` against the same acceptance checks).

### Summary

Fix Planner is the repair-loop entry point. After UAT verification produces concrete failures, it deterministically loads the persisted UAT results and issue records, selects a tightly scoped set of fixes that fit the atomic 2–3-task model, and writes a verification-driven fix plan as persisted task plan artifacts. Execution then proceeds via `execute-plan`, followed by re-running `verify-work` to confirm the same acceptance checks now pass.
