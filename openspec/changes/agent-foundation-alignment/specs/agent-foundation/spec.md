# agent-foundation Specification

## Purpose
Define the deterministic AOS control-plane contract for orchestrating planning, execution, verification, continuity, and milestone progression from canonical workspace artifacts.

## ADDED Requirements
### Requirement: Artifact-driven orchestration gate
The system SHALL determine the next control-plane action from canonical persisted artifacts under `.aos/spec/**`, `.aos/state/**`, `.aos/evidence/**`, and `.aos/codebase/**`.

#### Scenario: Missing project specification
- **WHEN** `.aos/spec/project.json` is absent
- **THEN** the orchestrator routes to the new-project interview workflow

#### Scenario: Missing or stale codebase intelligence for non-new workspace
- **WHEN** `.aos/spec/project.json` is present (workspace is not new)
- **AND** `.aos/codebase/map.json` is absent or carries a staleness marker
- **AND** the next required step is roadmap generation or phase planning
- **THEN** the orchestrator routes to brownfield codebase mapping before proceeding

#### Scenario: Missing roadmap
- **WHEN** `.aos/spec/project.json` is present, codebase intelligence is confirmed present and fresh, and `.aos/spec/roadmap.json` is absent
- **THEN** the orchestrator routes to roadmap generation

#### Scenario: Missing task execution contract
- **WHEN** the current cursor identifies work to perform and no executable task plan exists for the target task
- **THEN** the orchestrator routes to planning rather than execution

#### Scenario: Verification pending after execution
- **WHEN** task execution evidence exists and the current task has not yet been verified
- **THEN** the orchestrator routes to verification

### Requirement: Task plans are the atomic execution contract
The system SHALL use task-scoped `plan.json` artifacts as the only atomic execution contract for execution, verification, fix reruns, and commit scope enforcement.

#### Scenario: Execute task from task plan
- **WHEN** a task is selected for execution
- **THEN** the executor reads `.aos/spec/tasks/{taskId}/plan.json`
- **AND** uses its file scopes, steps, and verification metadata as the execution contract

#### Scenario: Phase decomposition does not satisfy execution gate
- **WHEN** a phase-level planning artifact exists but no task-level plan exists for the target task
- **THEN** the orchestrator does not treat the phase artifact as sufficient for execution

### Requirement: Interactive project interview
The system SHALL run a real persisted interview loop before generating `project.json` for a new workspace.

#### Scenario: Conduct new-project interview
- **WHEN** the orchestrator dispatches the new-project interview workflow
- **THEN** the workflow collects question/answer state across persisted interview artifacts
- **AND** writes canonical project artifacts and interview evidence on completion

### Requirement: Brownfield codebase preflight
The system SHALL route non-new workspaces through a codebase intelligence check before roadmap generation.

#### Scenario: Codebase intelligence absent for non-new workspace
- **WHEN** `.aos/spec/project.json` is present and `.aos/codebase/map.json` is absent
- **THEN** the orchestrator routes to the Codebase Mapper before roadmap generation

#### Scenario: Codebase intelligence stale for non-new workspace
- **WHEN** `.aos/spec/project.json` is present and `.aos/codebase/map.json` carries a staleness marker
- **THEN** the orchestrator routes to the Codebase Mapper to refresh the pack before roadmap generation

#### Scenario: Codebase intelligence present and fresh
- **WHEN** `.aos/codebase/map.json` is present and not stale
- **THEN** the orchestrator proceeds to roadmap generation using the confirmed pack as a grounding input

### Requirement: Verification failure creates a fix loop
The system SHALL convert verification failure into a structured fix-planning and re-execution loop.

#### Scenario: Verification fails
- **WHEN** required verification checks fail for a task
- **THEN** the orchestrator dispatches fix planning
- **AND** the fix plan produces contracted fix-task artifacts
- **AND** the orchestrator routes the resulting fix task back through execution and verification

### Requirement: Successful verification advances roadmap progression
The system SHALL advance the workflow after successful verification using explicit phase and milestone progression logic.

#### Scenario: Verification passes and more work remains
- **WHEN** the current task verifies successfully and additional task or phase work remains in the roadmap
- **THEN** the orchestrator routes to the next planning or execution step required by the persisted roadmap state

#### Scenario: Verification passes and milestone is complete
- **WHEN** the current task verifies successfully and the current phase or milestone is complete
- **THEN** the orchestrator records milestone progression and advances the workflow cursor explicitly

### Requirement: Task-scoped atomic commit integration
The system SHALL support atomic git commits scoped to the contracted files of a completed task.

#### Scenario: Commit verified task outputs
- **WHEN** a task completes successfully and commit behavior is enabled for the workflow
- **THEN** the system stages only files within the task's allowed scope that were modified by execution
- **AND** records the resulting commit as task evidence

### Requirement: Orchestrator-owned continuity persistence
The system SHALL persist run evidence, state transitions, event log updates, and continuity/history outputs after every control-plane step.

#### Scenario: Persist control-plane step outputs
- **WHEN** any planning, execution, verification, or fix-planning step completes
- **THEN** the orchestrator updates the canonical run record
- **AND** appends the corresponding event entries
- **AND** updates the workflow state snapshot
- **AND** persists continuity/history outputs needed for resume and progress reporting

### Requirement: Canonical context-pack resolution
The system SHALL build context packs from canonical AOS contract paths without duplicating the `.aos` root prefix.

#### Scenario: Build task context pack
- **WHEN** the orchestrator or subagent workflow builds a task context pack
- **THEN** the pack includes the required driving task artifact first
- **AND** resolves contract paths from the canonical AOS root exactly once

#### Scenario: Optional artifacts exceed budget
- **WHEN** optional context-pack artifacts would exceed the configured pack budget
- **THEN** the system truncates optional artifacts at a stable boundary
- **AND** still includes the required driving artifact