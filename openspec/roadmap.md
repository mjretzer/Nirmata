# UI Capability Proposal Roadmap

## Purpose

This roadmap turns the post-foundation UI alignment work into a proposal-driven sequence.

Rule of execution:
- Each phase below is its own OpenSpec proposal.
- Each proposal is scoped to a user-visible capability, not to a single page.
- A proposal may touch multiple pages when that capability crosses page boundaries.
- Hook and API contract updates belong to the earliest phase that needs them unless backend drift becomes large enough to justify its own proposal.

## Current State

### Phase 0: Agent foundation alignment
- Proposal: `agent-foundation-alignment`
- Status: complete
- Purpose: align the AOS control plane to canonical artifacts, task-plan execution, fix-loop routing, milestone progression, continuity persistence, and brownfield preflight gating.
- Why it matters for UI work: every proposal below depends on these control-plane transitions being real and deterministic.

## Sequencing Principles

1. Surface gate state before refining page-specific flows that depend on it.
2. Make task-level planning visible before deepening verification and fix-loop UX.
3. Treat continuity and evidence as a user-visible capability, not as background plumbing.
4. Keep each proposal narrow enough to validate independently, but wide enough to cover the full user-visible capability end to end.

## Phased Proposal Plan

### Phase 1: Gate Status Surface
- Proposed change id: `gate-status-surface`
- Primary capability: show the orchestrator gate sequence as a first-class workspace status surface.
- User-visible outcome: users can see where a workspace is in the flow from interview through milestone completion without inferring it from scattered page state.
- Pages likely involved: `WorkspaceDashboard`, `ChatPage`, `CodebasePage`.
- Scope:
  - expose current gate state and next required step from canonical artifacts
  - show brownfield preflight and codebase-map readiness in the UI
  - add clear route-to-action entry points for the current blocking gate
- Exit criteria:
  - a user can open a workspace and understand the current gate and next action without reading raw artifacts
  - the brownfield preflight state is visible when it blocks roadmap or planning work

### Phase 2: Interview Flow UX
- Proposed change id: `interview-flow-ux`
- Primary capability: provide a real persisted project interview experience in the UI.
- User-visible outcome: the new-project interview feels like a resumable workflow rather than a simulated chat exchange.
- Pages likely involved: `ChatPage`, `WorkspaceDashboard`.
- Scope:
  - render persisted interview prompts and answers
  - make incomplete interview state resumable and explicit
  - surface interview completion and transition into roadmap generation cleanly
- Exit criteria:
  - a new workspace can be interviewed across multiple turns and resumed later without losing context
  - completion visibly advances the workspace out of the interview gate
- Dependency note: Phase 1 should land first so the interview state has a consistent top-level status surface.

### Phase 3: Task Plan Visibility
- Proposed change id: `task-plan-visibility`
- Primary capability: make task plans the primary planning surface in the UI.
- User-visible outcome: users can inspect task-level execution contracts directly instead of relying on phase-level summaries as if they were executable work.
- Pages likely involved: `PlanPage`, `VerificationPage`.
- Scope:
  - treat `.aos/spec/tasks/{taskId}/plan.json` as the canonical plan lens in the UI
  - show file scope, execution steps, definition of done, and verification metadata from the task plan
  - distinguish clearly between phase decomposition artifacts and task execution artifacts
- Exit criteria:
  - the plan experience makes it obvious what is executable now versus what is only decomposition context
  - verification views can reference the same task-level contract users saw in planning

### Phase 4: Verification And Fix Loop UX
- Proposed change id: `verification-fix-loop-ux`
- Primary capability: represent verification failure and fix reruns as a first-class workflow.
- User-visible outcome: users can follow a task from verification failure into fix planning, re-execution, and re-verification without route confusion.
- Pages likely involved: `VerificationPage`, `FixPage`, `RunsPage`.
- Scope:
  - show explicit verify-failed, fix-planned, fix-executing, and re-verified states
  - surface fix tasks and their relationship to the original task
  - make rerun evidence discoverable from the verification surface
- Exit criteria:
  - a failed verification no longer feels like a generic fallback path in the UI
  - the user can tell what fix work was generated, what ran, and whether it cleared verification
- Dependency note: Phase 3 should land first so fix work can reference canonical task plans.

### Phase 5: Milestone And Progression Surface
- Proposed change id: `milestone-progression-surface`
- Primary capability: show explicit roadmap progression after successful verification.
- User-visible outcome: users can see when a task completion advanced a phase or milestone and what work became current next.
- Pages likely involved: `WorkspaceDashboard`, `PlanPage`.
- Scope:
  - surface completed task, phase, and milestone transitions as explicit UI events
  - show next-task or next-phase progression driven from canonical artifacts
  - make milestone completion distinct from ordinary task success
- Exit criteria:
  - users can tell the difference between task completion and milestone completion
  - the dashboard and plan views agree on what work is current next
- Dependency note: this phase depends on the task-plan and verification/fix-loop surfaces being trustworthy.

### Phase 6: Evidence And Continuity Surface
- Proposed change id: `evidence-continuity-surface`
- Primary capability: expose deterministic continuity, history, and run evidence as a coherent user-visible capability.
- User-visible outcome: users can inspect the persisted record of what happened, not just the latest page state.
- Pages likely involved: `RunsPage`, `ContinuityPage`, `ChatPage`.
- Scope:
  - show continuity snapshots, run summaries, and history outputs backed by canonical artifacts
  - make event-stream-backed evidence discoverable from relevant task and chat flows
  - reduce silent gaps between what ran and what the UI can explain afterward
- Exit criteria:
  - users can trace a task lifecycle through runs, continuity output, and history without dropping to raw files
  - the UI reflects deterministic post-step persistence rather than best-effort page-local state
- Dependency note: this phase can begin after foundation alignment, but it will read more clearly once Phases 3 through 5 establish the visible planning and execution flow.

## Suggested Order

1. `gate-status-surface`
2. `interview-flow-ux`
3. `task-plan-visibility`
4. `verification-fix-loop-ux`
5. `milestone-progression-surface`
6. `evidence-continuity-surface`

## Proposal Authoring Guidance

When opening each proposal:
- keep the capability end to end, even if it spans multiple pages
- avoid page-by-page proposals unless a page truly owns the entire user-visible behavior alone
- tie requirements back to canonical AOS artifacts and transitions introduced by `agent-foundation-alignment`
- include frontend hook or contract changes inside the proposal when they are necessary to realize the capability

## Out Of Scope For This Roadmap

- one proposal per page
- a separate proposal only for `useAosData` hook cleanup
- visual redesign work that does not map to a control-plane-backed user capability