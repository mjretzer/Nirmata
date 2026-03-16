## Context
The orchestrator currently depends on `.aos/state/state.json` and `.aos/state/events.ndjson` being present and coherent before workflow routing and execution. While `aos init` seeds these artifacts, runtime paths can still encounter missing or stale state and produce hard failures instead of recoverable agent behavior.

This change adds deterministic runtime preflight semantics so state readiness is enforced before execution and degraded states are repaired or surfaced conversationally.

## Goals / Non-Goals
- Goals:
  - Guarantee deterministic baseline state artifacts exist before write-capable workflow execution.
  - Provide a deterministic path to derive snapshot state from ordered events when snapshot is missing or stale.
  - Convert unrecoverable readiness failures into conversational guidance with actionable next steps.
- Non-Goals:
  - Redesigning the broader gating phase model.
  - Introducing new schema versions for state/event contracts.
  - Replacing existing `aos init` bootstrap semantics.

## Decisions
- Decision: Introduce a runtime startup hook (`EnsureWorkspaceInitialized`) in orchestrator preflight.
  - Rationale: Ensures every write-capable run has a consistent, deterministic state baseline independent of manual init timing.
- Decision: Repair missing baseline artifacts in-place (create missing `state.json`, ensure `events.ndjson` exists).
  - Rationale: Missing baseline files are safe and deterministic to create; this avoids unnecessary hard failures.
- Decision: Support deterministic snapshot derivation from event log when snapshot is missing or stale.
  - Rationale: Event log is authoritative append-only history; deriving snapshot restores consistency without nondeterministic behavior.
- Decision: Surface unrecoverable preflight failures through conversational orchestrator output with a suggested fix command.
  - Rationale: Keeps agent UX aligned with existing conversational recovery patterns and prevents opaque "Snapshot not set" style failures.

## Risks / Trade-offs
- Risk: Auto-repair could hide deeper state corruption.
  - Mitigation: Restrict repair to deterministic baseline creation and explicit derive-from-events behavior; emit diagnostics on failure.
- Risk: Extra preflight work may add latency.
  - Mitigation: Keep checks idempotent, file-local, and no-op when state is already healthy.

## Migration Plan
1. Add/update state-store and preflight validation behavior to support deterministic readiness checks and repair.
2. Integrate startup hook in orchestrator execution flow before phase dispatch.
3. Add regression tests for missing snapshot/log, stale snapshot derivation, and conversational failure handling.
4. Validate OpenSpec change strictly before implementation handoff.

## Open Questions
- What exact stale detection heuristic should preflight use initially (e.g., missing cursor fields vs. event tail mismatch)?
- Should derive-from-events run for read-only requests, or only for write-capable intents?
