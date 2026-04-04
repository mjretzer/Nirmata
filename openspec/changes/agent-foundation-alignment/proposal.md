## Why

The repository already contains most of the agent foundation pieces called for by the AOS design: an orchestrator, subagent execution, planning agents, verification, fix planning, continuity services, state persistence, and run evidence. The current problem is not absence of components; it is that the components are not yet aligned into the strict, artifact-driven control loop the foundation is supposed to guarantee.

Today there are several contract mismatches and partial implementations that make the control plane less deterministic than intended:

- the orchestrator gate logic mixes phase-level and task-level planning assumptions
- the new-project interviewer exists but simulates Q&A instead of running a real interview loop
- fix planning and atomic commits exist but are not fully treated as first-class orchestration steps/hooks
- milestone completion and next-phase progression are not modeled as explicit control-plane transitions
- continuity and history services exist, but post-step persistence is not consistently enforced by the orchestrator
- context pack and artifact path handling are not fully normalized to canonical `.aos` paths
- the orchestrator gate has no brownfield codebase intelligence check, so roadmap generation and planning proceed without guaranteed grounding in the actual repository when `.aos/codebase/**` is absent or stale

This change aligns the agent foundation to the intended spec-first workflow so the orchestrator can make routing decisions from canonical artifacts on disk rather than from partial heuristics or loosely coupled handlers.

## What Changes

- Define a single `agent-foundation` capability for the control-plane contract that governs orchestration, task-scoped execution, verification/fix loops, milestone progression, and continuity persistence.
- Align the orchestrator gate sequence to canonical on-disk artifacts under `.aos/spec`, `.aos/state`, `.aos/evidence`, and `.aos/codebase`.
- Make task plans, not phase-level pseudo-plans, the atomic execution contract for the executor, verifier, and fix loop.
- Formalize the missing or partial roles in the loop:
  - interactive new-project interviewing
  - explicit milestone progression after successful verification
  - first-class fix-plan rerouting
  - task-level atomic commit integration
  - orchestrator-owned continuity/history updates after each transition
- Normalize context pack construction and path resolution to canonical AOS contract paths.
- Add the brownfield codebase preflight gate: check for `.aos/codebase/map.json` presence and freshness before routing to roadmap generation for non-new workspaces; route to Codebase Mapper when the pack is absent or stale.

## Capabilities

### New Capabilities

- `agent-foundation`: Define the deterministic artifact-driven control plane for planning, execution, verification, continuity, and progression across phases and milestones.

### Modified Capabilities

- `project-management`: Clarify that project progression is controlled through persisted AOS artifacts and strict orchestrator gating rather than inferred chat/session state.

## Impact

- Backend:
  - `nirmata.Agents` control-plane, planning, execution, verification, fix-planning, and continuity components.
  - `nirmata.Aos` state/evidence/context-pack infrastructure where canonical path and event/state behavior are enforced.
- Architecture:
  - establishes one authoritative gate order for spec-first execution
  - clarifies the distinction between phase decomposition artifacts and task execution artifacts
  - closes the gap between specialist agent implementations and the intended orchestration contract
- Tests:
  - new control-plane transition coverage
  - artifact contract and path normalization tests
  - integration tests for verify/fix/commit/continue transitions