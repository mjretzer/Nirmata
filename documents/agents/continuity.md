# Continuity & Resumability Agents

Source: Continuity__Resumability.pdf (Section 8)

---

## Pause/Resume Manager Agent

### Responsibilities

- Enable interruption-safe work by persisting an explicit handoff snapshot mid-phase.
- Restore session continuity from the latest persisted snapshot without relying on chat history.
- Resume a specific interrupted subagent execution by execution id (run-level resumability).
- Keep cursor, blockers, and next actions consistent with `.aos/state/**` and run evidence under `.aos/evidence/**`.

### Step Format (single run)

1. Receive intent: `pause-work`, `resume-work`, or `resume-task <EXECUTION-ID>` (CLI equivalent).
2. Load operational truth from `.aos/state/state.json` and locate latest run metadata under `.aos/evidence/runs/**` (and task-evidence pointers if resuming a task).
3. If **pause-work**: write a handoff snapshot artifact capturing cursor, in-flight task/step, allowed scope, pending verification, and next command; persist it to `.aos/state/handoff.json` (and append `work.paused` to `.aos/state/events.ndjson`).
4. If **resume-work**: load `.aos/state/handoff.json`, confirm it matches current spec/roadmap cursor, rebuild the minimal context pack for the next action, and append `work.resumed` to events.
5. If **resume-task \<id\>**: locate the matching RUN-* record, restore the execution packet + context pack reference, and dispatch the subagent continuation; append `task.resumed` to events.
6. Return control to Orchestrator with a single next action (continue execution, re-verify, or re-plan if the snapshot no longer matches current artifacts).

### Summary

Pause/Resume Manager makes long-horizon execution robust by persisting explicit handoff state when work stops and restoring it deterministically later. It writes a durable handoff snapshot (`.aos/state/handoff.json`) for `pause-work`, reconstructs the next runnable context for `resume-work`, and can restart an interrupted subagent execution by id by reloading the corresponding RUN-* evidence and execution packet. All transitions are recorded in `.aos/state/events.ndjson` so resumption is artifact-driven and auditable.

---

## Progress Reporter Agent

### Responsibilities

- Answer "Where am I? What's next?" using **only canonical artifacts** (no chat inference).
- Determine current cursor from `.aos/state/state.json` (milestone/phase/task/step + status).
- Determine in-flight vs upcoming work from `.aos/spec/roadmap.json` and any active task plans under `.aos/spec/tasks/**`.
- Surface open loops that may affect routing: open issues in `.aos/spec/issues/**` and outstanding captured items under `.aos/context/todos/**` (or equivalent capture queue).
- Recommend the single next command/mode consistent with gating (plan → execute → verify → fix).

### Step Format (single run)

1. Receive `progress` intent (CLI equivalent).
2. Load `.aos/state/state.json` to read current position, blockers, and last transition.
3. Load `.aos/spec/roadmap.json` and the current task/phase artifacts (e.g., `.aos/spec/tasks/TSK-*/plan.json` status) to determine what is active vs what is next.
4. Scan for open loops: unresolved issues in `.aos/spec/issues/**` and any queued TODO/capture artifacts under `.aos/context/todos/**` (or configured capture directory).
5. Output a deterministic progress report: current cursor, completed vs remaining, blockers/issues, and the recommended next command (`plan-phase` / `execute-plan` / `verify` / `fix` / next phase).

### Summary

Progress Reporter is the navigation instrument for the system. It reads the persisted operational cursor (`.aos/state/state.json`) and cross-references the roadmap and active plans (`.aos/spec/roadmap.json`, `.aos/spec/tasks/**`) to report exactly where work stands and what the next correct command should be, while optionally surfacing open issues and queued TODO items that might change prioritization.

---

## History Writer Agent

### Responsibilities

- Maintain durable memory across sessions by updating operational state (`.aos/state/state.json` + `.aos/state/events.ndjson`) with decisions, blockers, and cursor position.
- Produce/refresh the authoritative narrative history (`.aos/spec/summary.md` or `.aos/evidence/summary.md` — one canonical location) describing what changed and why.
- Preserve traceability by aligning summaries to atomic task completion (one coherent unit of change per task/run).
- Ensure summaries reference concrete evidence (run ids, task ids, commit hashes, verification outputs) so narrative is auditable.

### Step Format (single run)

1. Trigger after a task or plan completes (post-verification), using the finished RUN-* record and associated TSK-* ids.
2. Load current cursor and memory from `.aos/state/state.json` and the relevant evidence from `.aos/evidence/runs/RUN-*/` (and `task-evidence/latest.json`).
3. Synthesize a concise change record: what was done, which files changed, how it was verified, and any new decisions/blockers discovered.
4. Write/refresh the narrative summary artifact (append the new entry keyed by date + RUN/TSK ids) and link to evidence/commit references.
5. Update operational memory: append an event (`aos event append history.written <filejson>`) and advance position/blockers/decisions in `.aos/state/state.json` to reflect the completed work.
6. Return control to Orchestrator with the next transition recommendation (continue next task, `verify-work`, `fix-plan`, or next phase).

### Summary

History Writer is the continuity layer that makes the system resumable without re-deriving context from diffs. After verified execution, it converts run/task evidence into a concise, human-readable narrative entry (what happened, what changed, how it was verified) and updates the durable operational memory (decisions, blockers, cursor) in `.aos/state/**`. By tying each summary entry to task ids, run ids, and commit/verification evidence, it preserves traceability and keeps future sessions grounded in explicit artifacts.
