## Context

Nirmata already has a substantial AOS substrate in place. `Orchestrator`, `SubagentOrchestrator`, `Roadmapper`, `PhasePlanner`, `TaskExecutor`, `UatVerifier`, `FixPlanner`, `PauseResumeManager`, `ProgressReporter`, `HistoryWriter`, and the state/evidence stores all exist in code. The misalignment is structural: some components still operate on simplified assumptions, some transitions are missing from the central loop, and some artifact boundaries are inconsistent.

The most important mismatch is that the system intends task plans to be the atomic unit of execution, but parts of the orchestrator still reason about phase-level plan existence. That weakens the gate model and makes downstream behavior less deterministic than the architecture documents describe.

This proposal does not introduce a new agent system from scratch. It aligns the existing one to the intended control-plane contract.

## Goals / Non-Goals

**Goals:**
- Make orchestrator delegation fully artifact-driven from `.aos/spec/**`, `.aos/state/**`, and `.aos/evidence/**`.
- Define one strict gate order for planning, execution, verification, fix planning, and milestone progression.
- Treat task plans as the only atomic execution contract used by executor, verifier, and fix reruns.
- Make the new-project interview a real interactive workflow rather than a simulated placeholder.
- Ensure each completed control-plane step persists state/events/evidence/history through orchestrator-owned hooks.
- Normalize context-pack resolution to canonical AOS contract paths.
- Route non-new workspaces through a brownfield codebase preflight gate before roadmap generation when `.aos/codebase/map.json` is absent or its freshness marker is stale; the orchestrator must never start roadmap work without confirmed codebase grounding.

**Non-Goals:**
- Redesigning frontend pages or domain API routes.
- Replacing the current artifact formats wholesale.
- Making brownfield codebase mapping a required gate on every run for every workspace type.
- Introducing chat-history-derived orchestration state.

## Decisions

### Decision 1: The orchestrator gate must be driven only by persisted artifacts
- Choice: Gate evaluation will derive routing exclusively from canonical artifacts on disk rather than from inferred conversational state or handler-local assumptions.
- Rationale: The foundation is supposed to be resumable and auditable. That only holds if routing can be reproduced from `.aos/spec`, `.aos/state`, and `.aos/evidence` alone.
- Alternatives considered:
  - Continue using a mixed model of persisted state plus convenience heuristics in handlers. Rejected because it makes replay and pause/resume behavior ambiguous.

### Decision 2: Task plans are the atomic execution contract
- Choice: `plan.json` under `.aos/spec/tasks/<taskId>/plan.json` is the only execution contract consumed by the task executor, verifier, fix reruns, and commit scope logic.
- Rationale: The existing executor, verifier, and commit code already depend on task-level plans. The control plane must align to that instead of checking a separate phase-level plan as if it were executable work.
- Alternatives considered:
  - Keep both phase-level and task-level plans as execution candidates. Rejected because it creates competing sources of truth.

### Decision 3: Phase plans remain decomposition artifacts, not execution artifacts
- Choice: Phase-level planning artifacts may summarize decomposition, but they do not satisfy the executor gate by themselves.
- Rationale: A phase can contain multiple task contracts with distinct file scopes and verification steps; only task artifacts are precise enough for contracted execution.

### Decision 4: Milestone progression is an explicit control-plane transition
- Choice: After a task verifies successfully, the orchestrator must either dispatch the next task/phase planner or invoke milestone progression logic when the current phase/milestone is complete.
- Rationale: The current loop stops short of the end-to-end roadmap progression the architecture describes.
- Alternatives considered:
  - Infer milestone completion indirectly from UI/API consumers. Rejected because progression belongs in the control plane.

### Decision 5: Fix planning and atomic commit become first-class orchestration hooks
- Choice: Verification failure must dispatch `FixPlanner` through a dedicated orchestrator path, and successful task completion may trigger task-scoped atomic commit logic as an explicit post-execution hook.
- Rationale: Both capabilities already exist in code; the gap is that they are not consistently owned by the central workflow.

### Decision 6: Continuity and history updates are orchestrator responsibilities
- Choice: Run evidence, state updates, event append, and history/continuity snapshots must be finalized as part of orchestrator-controlled transitions rather than left to scattered best-effort updates.
- Rationale: Continuity is foundational behavior, not an optional side effect.

### Decision 7: Brownfield codebase preflight runs before roadmap generation when intelligence is absent or stale
- Choice: After the new-project interview gate, the orchestrator checks for `.aos/codebase/map.json`. If the file is absent or carries a staleness marker, the orchestrator routes to the Codebase Mapper before proceeding to roadmap generation. Once the pack is confirmed present and fresh the gate passes and the brownfield check does not repeat until the marker is reset.
- Rationale: Planning agents that generate a roadmap without codebase grounding must guess at structure, conventions, and technology details. The preflight check eliminates that dependency on assumptions without requiring a rescan on every run.
- Alternatives considered:
  - Always require a fresh codebase scan before every roadmap generation. Rejected: rescanning is expensive and unnecessary when the repo has not materially changed.
  - Leave the brownfield check entirely to external tooling. Rejected: it leaves the orchestrator gate incomplete; the intelligence must be confirmed present before roadmap work begins.

### Decision 8: Context packs must resolve canonical contract paths without path duplication
- Choice: Context packs will be built from canonical `.aos/...` contract paths relative to the AOS root and must always include the required driving artifact.
- Rationale: Incorrect path normalization breaks deterministic context assembly and undermines subagent isolation.

## Risks / Trade-offs

- **Control-plane refactoring touches many integration points** → Mitigation: keep the change centered on routing contracts, artifact resolution, and transition ownership rather than broad rewrites.
- **Existing tests may encode current, incorrect assumptions** → Mitigation: update tests to assert the new gate order and task-plan contract explicitly.
- **Real interactive interviewing introduces UI/CLI dialogue coordination concerns** → Mitigation: scope this change to a true persisted interview loop contract without redesigning the chat UX.
- **Post-step hooks may surface latent persistence bugs** → Mitigation: add focused integration coverage around state.json, events.ndjson, run evidence, and summary/history output.

## Migration Plan

1. Define the complete gate order including the brownfield codebase preflight check; pin the staleness detection rule for `.aos/codebase/map.json` so implementations agree on the trigger.
2. Normalize artifact contracts and gate evaluation around task-scoped execution artifacts.
3. Add the missing explicit control-plane transitions: interactive interviewing, brownfield preflight, milestone progression, first-class fix rerouting, and commit hooks.
4. Move continuity/history persistence into orchestrator-owned transition hooks.
5. Correct context-pack and canonical path handling.
6. Add transition, persistence, and artifact contract tests.

Rollback strategy:
- Revert the orchestrator gate changes first if transition behavior regresses.
- Keep artifact/path normalization changes isolated so they can be reverted independently from interviewer/progression changes if necessary.

## Resolved Questions

- **Atomic task commits**: Configurable per workspace via a `commitOnVerify` flag in workspace configuration, defaulting to `false`. The orchestrator checks this flag before invoking the commit hook. Implementers must not default to always-on or always-off without reading the flag.
- **Milestone progression implementation**: A lightweight transition method invoked by the orchestrator, not a standalone handler. Keeps progression logic centralized in the orchestrator's transition sequence rather than distributing it to a separate handler.
- **Brownfield codebase refresh trigger**: Automatic when `.aos/codebase/map.json` is absent or carries a staleness marker (see Decision 7). The orchestrator does not require explicit user opt-in; the preflight check runs as part of the non-new-workspace gate sequence before roadmap generation.