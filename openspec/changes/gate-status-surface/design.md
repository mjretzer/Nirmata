## Context

`agent-foundation-alignment` made the orchestrator gate sequence deterministic from canonical artifacts, but the frontend still treats that state as implicit context spread across page-specific data and failures. Phase 1 needs a shared status surface that can explain the current gate at the workspace level before later proposals deepen individual interview, plan, verification, and continuity experiences.

The affected pages are cross-cutting: `WorkspaceDashboard` needs a top-level status summary, `ChatPage` needs gate-aware context for the active workflow, and `CodebasePage` needs to explain brownfield preflight blockers and readiness. The design should therefore avoid page-local inference and instead standardize a single workspace gate summary that all three pages consume.

## Goals / Non-Goals

**Goals:**
- Expose the current gate, next required step, and blocking reason from canonical artifacts in a shared workspace status model.
- Surface brownfield preflight and codebase map readiness as explicit status details when they affect workflow progression.
- Provide consistent route-to-action affordances across dashboard, chat, and codebase pages.
- Keep the UI aligned with the gate order introduced by `agent-foundation-alignment` without changing that order.

**Non-Goals:**
- Redesigning page layouts beyond the status surface and its action affordances.
- Reworking the interview, planning, verification, or continuity page internals beyond what is needed to host the shared status surface.
- Changing orchestrator routing logic or introducing speculative client-side gate inference.
- Defining all later page-specific UX for interview, task planning, or fix-loop behavior.

## Decisions

### Decision 1: Derive a single workspace gate summary from canonical artifacts server-side
- Choice: expose a workspace-scoped gate summary read model derived from canonical `.aos/spec`, `.aos/state`, and `.aos/codebase` artifacts.
- Rationale: the same gate logic must appear consistently on multiple pages, and server-side derivation prevents each page from independently inferring state from partial data.
- Alternatives considered:
  - Let each page infer gate state from existing file/spec endpoints: rejected because the logic would drift and brownfield readiness would remain inconsistent.
  - Read raw artifact files directly in the frontend and compute status there: rejected because it duplicates orchestrator semantics in the client.

### Decision 2: Represent brownfield readiness as part of the gate summary instead of a separate UI-only check
- Choice: include codebase preflight details in the same summary contract as the current gate and next action.
- Rationale: the roadmap explicitly calls out codebase-map readiness as part of the blocking gate story, so it should not be modeled as a disconnected health badge.
- Alternatives considered:
  - Add a separate codebase-readiness widget with its own fetch path: rejected because users would still need to mentally combine two different status surfaces.

### Decision 3: Use shared page-level presentation with gate-specific action mapping
- Choice: keep one normalized status model and let each page render the same gate summary with a primary action that routes to the page or flow that resolves the blocker.
- Rationale: the capability spans `WorkspaceDashboard`, `ChatPage`, and `CodebasePage`, but users should see a coherent explanation everywhere they encounter the workflow.
- Alternatives considered:
  - Build a distinct status component per page: rejected because labels, priority, and action mapping would drift.
  - Restrict the status surface to the dashboard only: rejected because chat and codebase are the places where users will often encounter the blocker while working.

### Decision 4: Keep the contract focused on current state and next action, not full workflow history
- Choice: the summary exposes current gate, reason, readiness details, and route hints only.
- Rationale: this phase is about explaining what blocks progress now. History, evidence, and continuity are explicitly deferred to later roadmap phases.
- Alternatives considered:
  - Include run history or event-stream evidence in the same contract: rejected because it widens scope into the continuity proposal.

## Risks / Trade-offs

- Contract drift between orchestrator and UI summary -> derive the summary from the same canonical artifact rules documented in the foundation spec and cover transitions with tests.
- Status surface becomes too generic to be actionable -> include explicit route/action metadata and gate-specific blocking details, especially for brownfield preflight.
- Page duplication creeps back in during implementation -> centralize both the data hook and the gate-to-action mapping.
- Future phases need richer data than the summary exposes -> keep the summary additive and focused so later proposals can extend adjacent flows without breaking this surface.

## Migration Plan

1. Define the workspace gate summary contract and the UI requirements for rendering it.
2. Add backend derivation for current gate, next action, and brownfield readiness from canonical artifacts.
3. Add a shared frontend hook/component for the status surface and wire it into dashboard, chat, and codebase pages.
4. Verify the surface stays synchronized with gate transitions and brownfield readiness changes.

## Open Questions

- Should the workspace gate summary be exposed from a dedicated endpoint or folded into an existing workspace snapshot response, provided the contract remains canonical and shared?
- What is the exact freshness/staleness signal for `.aos/codebase/map.json` that the UI should display without exposing unnecessary implementation detail?