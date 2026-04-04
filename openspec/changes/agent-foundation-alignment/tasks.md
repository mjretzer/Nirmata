## 1. Normalize the control-plane contract

- [x] 1.1 Align orchestrator gate evaluation to canonical artifacts under `.aos/spec`, `.aos/state`, `.aos/evidence`, and `.aos/codebase` only.
- [x] 1.2 Remove the phase-plan/task-plan mismatch from the execution gate so task plans are the only atomic execution contract.
- [x] 1.3 Define and implement the strict gate order: new-project interview → brownfield codebase preflight (non-new workspaces only) → roadmap → phase/task planning → execution → verification → fix loop → next-phase progression → milestone completion.
- [x] 1.4 Implement the brownfield codebase preflight gate: after the new-project interview gate, check for `.aos/codebase/map.json` presence and freshness; route to Codebase Mapper when absent or stale; proceed to roadmap generation only when the pack is confirmed present and fresh.

## 2. Complete missing or partial specialist-agent behavior

- [x] 2.1 Replace simulated `NewProjectInterviewer` Q&A with a real persisted interview loop that writes canonical project artifacts and evidence.
- [x] 2.2 Add explicit milestone progression handling after successful verification when a phase or milestone completes.
- [x] 2.3 Route `FixPlanner` through a first-class orchestrator dispatch path instead of generic fallback routing.
- [x] 2.4 Wire task-scoped atomic commit behavior into the success path with contracted file-scope enforcement.

## 3. Make continuity a guaranteed post-step behavior

- [x] 3.1 Ensure every orchestrator transition closes its run record, validates outputs, and persists state/event updates deterministically.
- [x] 3.2 Wire history and continuity snapshot updates into orchestrator-owned post-step hooks rather than best-effort handler-local behavior.
- [x] 3.3 Verify pause/resume and progress reporting derive only from canonical artifacts produced by the updated loop.

## 4. Normalize context and path handling

- [x] 4.1 Fix context-pack creation and canonical path resolution so `.aos` contract paths are resolved exactly once from `AosRootPath`.
- [x] 4.2 Enforce driving-artifact inclusion and stable budget behavior for task and phase context packs.
- [x] 4.3 Add tests that protect against task/phase contract drift and path duplication regressions.

## 5. Validate the aligned foundation

- [x] 5.1 Add unit/integration coverage for orchestrator gate transitions, including verify-failed and verify-passed branches.
- [x] 5.2 Add tests for interactive interview persistence, task-plan execution routing, fix reruns, and milestone progression.
- [x] 5.3 Add evidence/state continuity tests covering `state.json`, `events.ndjson`, run summaries, and history output across a full task lifecycle.