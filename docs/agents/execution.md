# Execution Agents

Source: Execution.pdf (Section 3)

---

## Task Executor (Subagent)

### Responsibilities

- Execute the planned tasks sequentially, one atomic task at a time.
- Run each task in a **fresh subagent context** (no long-horizon accumulation) to preserve quality.
- Apply only the plan-specified actions to the plan-specified files (strict scope).
- Run verification steps and record evidence so completion is proven, not assumed.
- Keep changes atomic and coherent per task so evidence and rollback remain clean.

### Step Format (single run)

1. Receive `execute-plan` intent (CLI equivalent) and load the target task plans from `.aos/spec/tasks/TSK-*/plan.json` (and `.aos/state/state.json` plus minimal codebase notes if needed).
2. For the next planned task in cursor order: request a fresh subagent run from Subagent Orchestrator with only the task plan + allowed scope + minimal context pack.
3. Implement the plan actions strictly within the allowed files/paths and produce the required outputs.
4. Execute the plan verification steps (commands/checks) and capture outputs as evidence in `.aos/evidence/runs/RUN-*/`.
5. Emit a normalized result (pass/fail + verification evidence + scope-expansion flags) and update task status/cursor via State Manager, then proceed to the next task until the phase plan is complete.

### Summary

Task Executor is the implementation engine that turns persisted task plans into verified changes. It processes each atomic task sequentially, uses a fresh subagent context per task to prevent drift, edits only the explicitly allowed files, runs the defined verification checks, records auditable evidence in RUN-* folders, and reports a normalized pass/fail result back to the Orchestrator/State Manager so progress is tracked deterministically and verification is evidence-based.

---

## Atomic Git Committer Agent

### Responsibilities

- Enforce **one task = one commit** immediately after each task passes verification.
- Stage only the files that belong to the completed task (strict scope) to keep history surgical.
- Produce meaningful, consistent commit messages tied to task identifiers for traceability and future grounding.
- Capture commit metadata (hash, message, touched files) as evidence attached to the task/run.

### Step Format (single run)

1. Receive the task-complete signal from Task Executor, including the task id, allowed file list, and verification result (must be pass).
2. Determine the exact file set to stage (intersection of changed files and the task's allowed scope) and stage only those files.
3. Create the commit immediately using a standardized message format:
   - `feat(TSK-######): <short summary>`
   - `fix(TSK-######): <short summary>`
4. Persist proof by writing the commit hash + diffstat into the current run evidence (`.aos/evidence/runs/RUN-*/`) and linking it into `.aos/evidence/task-evidence/TSK-*/latest.json`.
5. Return the commit reference to Orchestrator so the next task can proceed with a clean working tree.

### Summary

Atomic Git Committer is a reliability control: after each task passes verification, it stages only the task-scoped changes and commits them immediately, preserving the invariant **one task = one commit**. This produces a bisectable, revertible, and highly traceable history, and it records commit metadata into evidence so future sessions can ground decisions in observable repository state rather than ambiguous multi-change diffs.
