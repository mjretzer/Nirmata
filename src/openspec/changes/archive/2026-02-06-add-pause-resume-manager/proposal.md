# Change: Add Pause/Resume Manager Workflow

## Why

The agent orchestration engine needs interruption-safe execution that can pause mid-workflow and resume deterministically. Without pause/resume capability, long-running tasks are vulnerable to process restarts, user interruptions, or system failures. A handoff snapshot captures cursor position, in-flight task context, scope constraints, and next commands—enabling reliable continuation from any interruption point.

## What Changes

- **ADDED** `PauseResumeManager` class in `nirmata.Agents/Execution/Continuity/PauseResumeManager/` to orchestrate pause and resume operations
- **ADDED** Handoff state model and serialization to `.aos/state/handoff.json`
- **ADDED** `pause-work` command that creates interruption-safe snapshots including cursor, task context, and scope
- **ADDED** `resume-work` command that reconstructs execution state from handoff snapshot
- **ADDED** `resume-task` by RUN ID capability to locate historical evidence and restore execution packet
- **ADDED** Integration with `IRunManager` to preserve run evidence for resume-by-run scenarios

## Impact

- **Affected specs:** New capability `agent-continuity` (no existing spec modifications required)
- **Affected code:** `nirmata.Agents/Execution/Continuity/**` (new directory structure)
- **Affected workspace:** `.aos/state/handoff.json`, `.aos/evidence/runs/RUN-*/` (read for resume)
- **Dependencies:** `engine-run-manager`, `orchestrator-workflow`, `aos-state-store`

## Related

- Roadmap item: PH-PLN-0011
