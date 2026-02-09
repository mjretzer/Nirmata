# phase-planning Specification

## Purpose
TBD - created by archiving change implement-phase-planner-workflows. Update Purpose after archive.
## Requirements
### Requirement: Phase Context Gatherer collects phase context
The system SHALL provide an `IPhaseContextGatherer` interface that collects contextual information about a roadmap phase to inform task planning.

The implementation MUST:
- Accept a phase reference (phase ID from roadmap)
- Read the phase specification from `.aos/spec/roadmap.json`
- Collect related project context from `.aos/spec/project.json`
- Gather codebase intelligence relevant to the phase scope
- Produce a phase brief document summarizing goals, constraints, and scope
- Persist planning decisions as events to `.aos/state/events.ndjson`

#### Scenario: Context gatherer produces phase brief
- **GIVEN** a workspace with valid project.json and roadmap.json
- **WHEN** the context gatherer is invoked for phase "PH-001"
- **THEN** it reads phase spec, collects relevant codebase context, and produces a phase brief

#### Scenario: Planning decisions are persisted to state
- **GIVEN** a context gatherer execution
- **WHEN** the gatherer makes planning decisions (scope boundaries, approach selection)
- **THEN** those decisions are appended to `.aos/state/events.ndjson` as decision events

### Requirement: Phase Planner decomposes phases into tasks
The system SHALL provide an `IPhasePlanner` interface that decomposes a roadmap phase into 2-3 atomic tasks with explicit file scopes and verification steps.

The implementation MUST:
- Accept a phase brief from the context gatherer
- Use LLM-based planning to decompose phase into atomic tasks
- Generate task specifications with:
  - Task ID (TSK-XXXX format)
  - Task description and goal
  - Explicit file scopes (which files to modify)
  - Verification steps (how to confirm success)
- Write task.json to `.aos/spec/tasks/{task-id}/task.json`
- Write plan.json to `.aos/spec/tasks/{task-id}/plan.json` with detailed scope
- Write links.json to `.aos/spec/tasks/{task-id}/links.json` with relationships

#### Scenario: Phase planner creates task specifications
- **GIVEN** a phase brief for phase "PH-001"
- **WHEN** the planner decomposes the phase
- **THEN** it creates 2-3 task directories under `.aos/spec/tasks/` with complete task.json, plan.json, and links.json

#### Scenario: plan.json contains explicit file scopes and checks
- **GIVEN** a generated task plan
- **WHEN** examining plan.json
- **THEN** it contains explicit `fileScopes` array with target files and `verificationChecks` array with validation steps

### Requirement: Phase Assumption Lister captures planning assumptions
The system SHALL provide an `IPhaseAssumptionLister` interface that captures assumptions made during planning for verification and audit purposes.

The implementation MUST:
- Accept the phase brief and generated task plans
- Extract implicit assumptions (e.g., "file X exists", "API Y behaves as documented")
- Generate an assumptions.md artifact documenting all assumptions
- Attach the snapshot to evidence under `.aos/evidence/runs/{run-id}/artifacts/assumptions.md`

#### Scenario: Assumptions are captured and attached to evidence
- **GIVEN** a completed planning phase with tasks generated
- **WHEN** the assumption lister is invoked
- **THEN** assumptions.md is written to the current run's artifacts folder with all identified assumptions

### Requirement: Phase Planner Handler integrates with orchestrator
The system SHALL provide a `PhasePlannerHandler` that integrates the phase planning workflows with the orchestrator's gating and dispatch system.

The handler MUST:
- Implement the handler pattern used by the orchestrator
- Accept a planning intent with phase reference
- Coordinate context gathering → planning → assumption listing
- Return a handler result indicating success/failure and next phase
- Integrate with `IRunLifecycleManager` for evidence capture

#### Scenario: Handler executes full planning workflow
- **GIVEN** a planning intent for phase "PH-001"
- **WHEN** the handler is invoked by the orchestrator
- **THEN** it executes context gatherer → planner → assumption lister sequence and returns success with tasks created

#### Scenario: Handler reports planning failures
- **GIVEN** a planning intent for an invalid phase
- **WHEN** the handler is invoked and planning fails
- **THEN** it returns failure result with error details and no state corruption

