# Phase & Milestone Management Agents

Source: Phase_Managment.pdf (Section 6)

---

## Milestone Manager Agent

### Responsibilities

- Own milestone lifecycle: create, discuss/refine, ship/close, and transition to the next milestone.
- Maintain roadmap modularity at the milestone boundary (clean "v1 done → v2 starts" without losing continuity).
- Ensure milestone transitions are state-driven and persisted:
  - `.aos/spec/**` = intended truth
  - `.aos/state/**` = operational cursor
  - `.aos/evidence/**` = proof
- Create milestone structure: milestone record plus its phases (and ensure tasks are correctly assigned to milestone/phase).
- Prevent drift: milestone status and next-milestone intent must be captured in persisted specs/state, not chat history.

### Step Format (single run)

1. Receive milestone intent: `"discuss next"`, `"complete current"`, or `"new milestone [name]"`.
2. Load `.aos/state/state.json` + `.aos/spec/roadmap.json` + current milestone (`aos spec milestone list/show`) to anchor decisions/blockers/position.
3. If **discuss** or **new**: capture next-milestone deltas (scope, constraints, priorities) and persist them via `aos spec milestone create <title> --id MS-####` or `aos spec milestone update <MS-####> <filejson>`.
4. If **new**: create initial phases bound to the milestone using `aos spec phase create <title> --milestone MS-####` (repeat as needed).
5. If **complete**: verify closure criteria (no open tasks for the milestone, or explicitly accepted exceptions), then mark milestone shipped via `aos spec milestone update <MS-####> <filejson>`.
6. Append a milestone transition event (`aos event append milestone.completed <filejson>`) and update cursor/position (`aos state rebuild`), then checkpoint (`aos checkpoint create`) when the milestone boundary is crossed.
7. Hand back to Orchestrator with the next action: plan the first phase of the new milestone (`aos spec phase show … → task planning/execution flow`).

### Summary

The Milestone Manager is the versioning control for the long-horizon loop. It uses the canonical roadmap and state to close out the current milestone as shipped, capture what changes in the next iteration, and create the next milestone and its phases using the spec system (`aos spec milestone *`, `aos spec phase create`). It then records a milestone transition event and advances the operational cursor in `.aos/state/**` so planning and execution can resume cleanly in the next milestone without relying on implied conversational context.

---

## Milestone Creator Agent

### Responsibilities

- Create a new milestone (e.g., "v2") in the spec system so work can continue with plan → execute on its phases.
- Define and persist the milestone's initial phases (ready for phase-by-phase planning).
- Preserve continuity by anchoring the new milestone to existing roadmap + current state (no "reset" drift).
- Establish a clean milestone boundary that pairs with milestone completion/discussion workflows.

### Step Format (single run)

1. Receive command `new-milestone [name]` (CLI equivalent) and normalize the milestone title + identifier (`MS-####`).
2. Load canonical context: `.aos/state/state.json` and `.aos/spec/roadmap.json` (plus existing milestones list) to ensure correct project alignment.
3. Create the milestone spec record using `aos spec milestone create <name> --id MS-####` and persist to `.aos/spec/milestones/MS-####.json`.
4. Generate the phase set for the milestone (e.g., `PH-1..PH-n`) and create them with `aos spec phase create <phase name> --milestone MS-####`, persisting to `.aos/spec/phases/PH-*/phase.json`.
5. Link milestone ↔ phases in roadmap structures (update `.aos/spec/roadmap.json` or milestone index as required) and validate (`aos validate spec`).
6. Append an event (`aos event append milestone.created <filejson>`) and update cursor/position so the next command can plan the first phase.

### Summary

Milestone Creator implements `new-milestone [name]` by creating a new milestone record in `.aos/spec/**`, generating and persisting its phases, linking them into the roadmap, and recording the transition in `.aos/state/**`. This establishes the next project version cleanly while preserving continuity from existing roadmap/state artifacts, enabling immediate return to the standard `plan-phase → execute-plan` loop on the new milestone.

---

## Milestone Context Gatherer Agent

### Responsibilities

- Capture operator intent for the next milestone: priorities, constraints, acceptance expectations, and non-goals.
- Prevent scope drift by aligning milestone direction before creating phases or modifying the roadmap.
- Persist clarified milestone direction into durable state (decisions) so downstream steps do not rely on chat history.
- Produce an explicit "milestone brief" usable by Milestone Creator and Roadmapper.

### Step Format (single run)

1. Receive command `discuss-milestone` (CLI equivalent) and bind it to the current milestone cursor from `.aos/state/state.json`.
2. Load canonical context: current position and decisions/blockers from `.aos/state/state.json`, plus current trajectory from `.aos/spec/roadmap.json` (and current milestone spec if present).
3. Elicit next-milestone inputs (vNext) as a short structured brief: goals, priorities, constraints, non-goals, and acceptance criteria.
4. Persist the brief as durable decisions: append an event (`aos event append milestone.intent.captured <filejson>`) and write the normalized decisions into `.aos/state/state.json` (optionally also create a milestone draft spec record).
5. Return a routing decision to Orchestrator: either proceed to `complete-milestone` then `new-milestone`, or directly to `new-milestone`, or route to Roadmap edits if the trajectory must change first.

### Summary

Milestone Context Gatherer implements `discuss-milestone` as the alignment checkpoint before versioning forward. It anchors on the current cursor and roadmap, collects a concise milestone brief (what changes, what stays, what matters most, and how "done" will be judged), persists that intent into `.aos/state/**` as durable decisions and events, and hands back a clear route to either ship the current milestone and/or create the next milestone with phases based on the captured intent.
