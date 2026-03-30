# Planning Agents

Source: Project_Initialization__Planning.pdf (Section 2)

---

## New-Project Interviewer Agent

### Responsibilities

- Run a tight Q&A loop to extract goals, constraints, tech preferences, risks, and edge cases until the project is fully specified.
- Produce the canonical project spec artifact (`AOS: .aos/spec/project.json`) that is always considered "must-load" context.
- In brownfield scenarios, leverage `.aos/codebase/**` maps so questions focus on incremental change (not re-discovery).
- Resolve conflicts and lock explicit assumptions so downstream roadmap/planning is deterministic.

### Step Format (single run)

1. Receive `new-project` intent (CLI equivalent) and open a run record to capture interview provenance in `.aos/evidence/runs/RUN-*/`.
2. Load any available brownfield context (`.aos/codebase/**`) plus existing state (`.aos/state/state.json` if present) to tailor interview prompts.
3. Ask the minimum set of targeted questions to eliminate material unknowns (goals, constraints, non-goals, success criteria, environment, integrations, data, security, timeline).
4. Normalize and reconcile: convert answers into explicit requirements, resolve conflicts (must-have vs nice-to-have), and lock assumptions/constraints.
5. Persist the canonical project spec to `.aos/spec/project.json` (schema-valid) and append an event (`aos event append project.created <filejson>`), then validate (`aos validate spec`).
6. Hand off to Orchestrator with the next gating action: create the roadmap (`create-roadmap`) or, if brownfield mapping is missing, route to `map-codebase` first.

### Summary

The New-Project Interviewer is the front door to the workflow. It conducts a focused, iterative interview — optionally grounded in existing `.aos/codebase/**` documentation — until the project intent, constraints, and success criteria are explicit and conflict-free. It then writes the single canonical project truth (`.aos/spec/project.json`) and records the creation event so roadmap generation and subsequent planning can proceed from persisted artifacts rather than conversational context.

---

## Roadmapper Agent

### Responsibilities

- Generate the long-horizon roadmap (`AOS: .aos/spec/roadmap.json`) as phases/milestones from start to finish.
- Initialize the operational memory/state (`.aos/state/state.json` + `.aos/state/events.ndjson`) so the workflow is resumable.
- Establish the baseline "where you're going / what's done" structure that later planning decomposes into per-phase and per-task plans.
- Ensure roadmap structure is schema-valid and references milestones/phases/tasks consistently.

### Step Format (single run)

1. Receive `create-roadmap` intent (CLI equivalent) and load `.aos/spec/project.json` as the authoritative vision/spec input.
2. Draft the end-to-end roadmap structure (milestones → phases → initial task placeholders) and write `.aos/spec/roadmap.json` plus any required stub records under `.aos/spec/milestones/**` and `.aos/spec/phases/**`.
3. Initialize operational state: create/overwrite `.aos/state/state.json` with decisions/blockers/position set to the first milestone/phase, and start `.aos/state/events.ndjson` with a `"roadmap.created"` event.
4. Validate outputs (`aos validate spec` and `aos validate state`) and attach evidence to a run record in `.aos/evidence/runs/RUN-*/`.
5. Hand off to Orchestrator with the next gating action: plan the first phase (`plan-phase PH-####`) and then generate task plans.

### Summary

The Roadmapper converts the canonical project vision (`.aos/spec/project.json`) into two durable foundations: an end-to-end roadmap (`.aos/spec/roadmap.json`) that defines the milestone/phase sequence from start to finish, and an initialized operational memory (`.aos/state/state.json` + events) that captures decisions, blockers, and the current position so the system can resume deterministically. This establishes the baseline required for the next loop step: planning the first phase into executable task plans.

---

## Phase Planner Agent

### Responsibilities

- Convert a single roadmap phase into a small, execution-ready set of atomic tasks (target: **2–3 tasks per phase**).
- Produce an unambiguous task plan artifact per task (`AOS: .aos/spec/tasks/TSK-*/plan.json`) that specifies:
  - Exact files/paths allowed
  - Step-by-step actions
  - Verification commands/checks
  - Definition-of-done criteria
- Keep plans intentionally small and independently verifiable to preserve execution quality and traceability.
- Respect decisions/blockers and current cursor from `.aos/state/**` when shaping tasks and constraints.

### Step Format (single run)

1. Receive `plan-phase <PH-####>` intent (CLI equivalent) and load `.aos/spec/project.json`, `.aos/spec/roadmap.json`, and `.aos/state/state.json`.
2. Isolate the target phase outcomes/boundaries from the roadmap and translate them into explicit deliverables and constraints for this planning run.
3. Decompose the phase into 2–3 atomic tasks, each independently completable, with clear file scope and a concrete verification method.
4. Persist tasks and plans: create `.aos/spec/tasks/TSK-*/task.json` and `.aos/spec/tasks/TSK-*/plan.json` (schema-valid), and link them to the phase via `.aos/spec/tasks/TSK-*/links.json` (or phase index).
5. Validate outputs (`aos validate spec`) and append a planning event (`aos event append phase.planned <filejson>`), updating cursor readiness in `.aos/state/state.json`.
6. Hand off to Orchestrator with the next gating action: execute the plan (`execute-plan`) using fresh subagent contexts per task.

### Summary

The Phase Planner is the precision planning layer. It takes one roadmap phase and produces a deliberately small set of atomic, independently verifiable tasks (typically 2–3). Each task plan is persisted as a structured spec (`.aos/spec/tasks/TSK-*/plan.json`) with explicit file scope, precise action steps, verification commands/checks, and definition-of-done criteria — so execution can run with minimal ambiguity and high reliability via fresh subagent runs.

---

## Phase Context Gatherer Agent

### Responsibilities

- Capture phase-specific constraints, dependencies, and acceptance expectations immediately before planning.
- Surface hidden assumptions early to reduce rework during plan generation and execution.
- Persist phase clarifications into durable operational memory so planning is grounded in artifacts, not chat history.
- Produce a concise "phase brief" consumable by Phase Planner.

### Step Format (single run)

1. Receive `discuss-phase <PH-####>` intent (CLI equivalent) and bind it to the current cursor in `.aos/state/state.json`.
2. Load canonical context: phase definition from `.aos/spec/roadmap.json` (and phase spec if present), plus current decisions/blockers from `.aos/state/state.json` (project spec as needed).
3. Elicit missing specifics that materially change planning: scope boundaries, priorities, non-goals, dependencies, verification expectations, and "done" signals.
4. Persist clarifications as durable decisions: append an event (`aos event append phase.intent.captured <filejson>`) and write normalized constraints/priorities into `.aos/state/state.json` (optionally also update the phase spec record).
5. Hand off to Orchestrator with the next gating action: run `plan-phase <PH-####>` using the updated state as grounding.

### Summary

Phase Context Gatherer implements `discuss-phase` as the pre-planning alignment checkpoint. It anchors on the target phase's roadmap definition and the current operational state, captures the extra constraints and acceptance expectations that would affect task decomposition, persists those clarifications into `.aos/state/**` as durable decisions/events, and routes directly into `plan-phase` so the resulting task plans reflect real-world boundaries and verification criteria.

---

## Phase Assumption Lister Agent

### Responsibilities

- Expose the system's current implicit assumptions for a specific phase before planning begins.
- Make hidden scope, constraints, dependencies, and verification expectations explicit so they can be corrected early.
- Reduce avoidable rework by converting "silent misunderstandings" into reviewable, persisted notes.
- Provide a clean handoff into `discuss-phase` and `plan-phase` with corrected grounding.

### Step Format (single run)

1. Receive `list-phase-assumptions <PH-####>` intent (CLI equivalent).
2. Load phase grounding from `.aos/spec/roadmap.json` (and phase spec if present) plus decisions/blockers from `.aos/state/state.json` (project spec as needed).
3. Generate a bounded assumptions list for that phase (scope boundaries, non-goals, dependencies, constraints, interfaces, verification expectations).
4. Persist the assumptions snapshot as evidence (attach to `.aos/evidence/runs/RUN-*/`) and optionally as a state note/event (`aos event append phase.assumptions.listed <filejson>`).
5. Hand off to Orchestrator/user path: correct assumptions via `discuss-phase <PH-####>` (and then run `plan-phase <PH-####>`).

### Summary

Phase Assumption Lister is a pre-planning sanity check that surfaces what the system is currently assuming about a given phase — scope, constraints, dependencies, and "done" expectations — based on the roadmap and persisted state. By making those assumptions explicit (and optionally persisting a snapshot), it enables fast correction before planning, reducing rework caused by unspoken misunderstandings.

---

## Phase Researcher Agent

### Responsibilities

- Perform phase-scoped ecosystem research for niche domains to reduce uncertainty before planning.
- Translate research into actionable constraints (tooling norms, integration patterns, security/compliance, operational realities).
- Persist findings as durable artifacts so planning is grounded in files, not chat history.
- Flag risks, unknowns, and decision points that must be resolved before `plan-phase`.

### Step Format (single run)

1. Receive `research-phase <PH-####>` intent (CLI equivalent) and load the phase goal/boundaries from `.aos/spec/roadmap.json` plus current constraints from `.aos/state/state.json`.
2. Define a phase-scoped research brief (questions to answer, required decisions, constraints to validate) and open a run record in `.aos/evidence/runs/RUN-*/`.
3. Execute targeted research and synthesize outputs into "planning-grade" findings (constraints, recommended approaches, tool choices, known pitfalls, and verification norms).
4. Persist results as a research context pack under `.aos/context/packs/PH-####-research.json` and append a decision/risk event to `.aos/state/events.ndjson` (update `.aos/state/state.json` decisions/blockers if warranted).
5. Hand off to Orchestrator with the next gated action: `discuss-phase <PH-####>` (if decisions required) or directly `plan-phase <PH-####>` using the research pack as grounding.

### Summary

Phase Researcher is the de-risking workflow for niche or uncertain phases. It anchors research to the specific outcomes of `PH-####`, produces actionable domain constraints and recommended implementation/verification norms, persists those findings into `.aos/context/packs/**` and state decisions/events, and then routes back into the standard planning path so `plan-phase` generates tasks based on ecosystem reality rather than assumptions.
