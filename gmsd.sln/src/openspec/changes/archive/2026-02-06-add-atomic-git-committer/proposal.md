# Change: Add Atomic Git Committer

## Why
The Execution Plane needs a deterministic, scope-aware Git committer that stages only task-scoped files and commits with traceable TSK-based messages. The `aos-task-evidence` spec already defines `gitCommit` and `diffstat` slots in task-evidence pointers, but there is no concrete workflow that populates these slots safely.

Without an atomic committer, task executions risk:
- Staging files outside the task scope (unintended side effects)
- Commits with non-traceable messages (lost lineage)
- Evidence gaps where commit metadata is not captured deterministically

This change implements the Atomic Git Committer workflow that computes the intersection of changed files and allowed file scopes, stages only that intersection, commits with a deterministic TSK-based message, and records the commit hash + diffstat into task evidence.

## What Changes
- **ADDED** `IAtomicGitCommitter` interface and `AtomicGitCommitter` implementation in `Gmsd.Agents.Execution.Execution.AtomicGitCommitter`
- **ADDED** Scope-intersection staging (only stages files that are both changed AND in allowed scope)
- **ADDED** TSK-based commit message generation (`TSK-0001: <summary>`)
- **ADDED** Deterministic commit evidence capture (hash, diffstat, timestamp)
- **ADDED** Task evidence pointer updates via `aos-task-evidence` infrastructure
- **ADDED** Per-run commit artifacts at `.aos/evidence/runs/RUN-*/artifacts/git-commit.json`
- **ADDED** Integration with `agents-task-executor` for file scope awareness
- **ADDED** Handler integration with orchestrator's gating system

## Impact
- **Affected specs:** `agents-atomic-git-committer` (new)
- **Related specs:** `aos-task-evidence`, `agents-task-executor`, `aos-evidence-store`, `aos-run-lifecycle`
- **Affected code:**
  - `Gmsd.Agents/Execution/Execution/AtomicGitCommitter/**`
- **Workspace outputs:**
  - `.aos/evidence/runs/RUN-*/artifacts/git-commit.json` — commit metadata per run
  - `.aos/evidence/runs/RUN-*/artifacts/git-diffstat.json` — diff statistics per run
  - `.aos/evidence/task-evidence/TSK-*/latest.json` — updated with `gitCommit` and `diffstat` slots

## Security & Safety Notes
- Forbidden files (outside scope) are NEVER staged, even if modified
- If intersection is empty, no commit is made and evidence records `gitCommit: null`
- All git operations are captured in evidence for auditability
