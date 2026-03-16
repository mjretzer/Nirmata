# Change: Add Progress Reporter and History Writer Workflows

## Why

The agent orchestration engine needs deterministic visibility into execution progress and a durable narrative record of completed work. Without structured progress reporting, users and downstream systems cannot understand current cursor position, blockers, or recommended next actions. Without a history writer, there is no durable, verifiable summary linking runs and tasks to their evidence artifacts and commit hashes—making audit and review impossible.

## What Changes

- **ADDED** `ProgressReporter` class in `nirmata.Agents/Execution/Continuity/ProgressReporter/` to generate deterministic progress reports from current state
- **ADDED** `HistoryWriter` class in `nirmata.Agents/Execution/Continuity/HistoryWriter/` to append narrative summaries keyed by RUN/TSK with evidence references
- **ADDED** `report-progress` command that outputs current cursor, blockers, and next recommended command
- **ADDED** `write-history` command that appends entry to `.aos/spec/summary.md` with verification proof and commit hash
- **ADDED** Progress report output contract: cursor position, blockers list, next recommended command, timestamp
- **ADDED** History entry schema: RUN/TSK key, timestamp, verification proof, commit hash (when available), evidence pointers
- **ADDED** Integration with `IStateStore` to read cursor and roadmap state for progress reports
- **ADDED** Integration with `IEvidenceStore` to locate evidence artifacts for history entries

## Impact

- **Affected specs:** New capability `agent-continuity` (extends existing with progress/history concerns)
- **Affected code:** `nirmata.Agents/Execution/Continuity/ProgressReporter/**`, `nirmata.Agents/Execution/Continuity/HistoryWriter/**`
- **Affected workspace:** `.aos/spec/summary.md` (history entries), `.aos/evidence/runs/RUN-*/summary.json` (read for evidence pointers)
- **Dependencies:** `engine-run-manager`, `aos-state-store`, `aos-evidence-store`, `orchestrator-workflow`

## Related

- Roadmap item: PH-PLN-0012
