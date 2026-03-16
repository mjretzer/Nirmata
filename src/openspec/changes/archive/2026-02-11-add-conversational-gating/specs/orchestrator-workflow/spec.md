## ADDED Requirements

### Requirement: Gating engine evaluates 6-phase routing logic with reasoning

The system SHALL provide an `IGatingEngine` interface that evaluates workspace state in priority order and produces explainable, confirmable routing decisions:

1. Missing project spec → route to **Interviewer**
2. Missing roadmap → route to **Roadmapper**
3. Missing phase plan → route to **Planner**
4. Ready to execute → route to **Executor**
5. Execution complete, verification pending → route to **Verifier**
6. Verification failed → route to **FixPlanner**

The gating result MUST include:
- `TargetPhase`: the selected phase identifier
- `Reasoning`: human-readable explanation of why this phase was selected
- `ProposedAction`: structured object describing what action will be taken
- `RequiresConfirmation`: boolean indicating if user confirmation is required

#### Scenario: Gating produces explainable routing decision

- **GIVEN** a workspace with project spec and roadmap, but no tasks planned for current phase at cursor
- **WHEN** `EvaluateAsync` is called
- **THEN** result indicates `TargetPhase: Planner`
- **AND** result includes `Reasoning` explaining the project exists but planning is incomplete
- **AND** result includes `ProposedAction` with type "CreatePhasePlan" and phaseId
- **AND** result includes `RequiresConfirmation: true` for write operations

#### Scenario: Gating identifies destructive operations requiring confirmation

- **GIVEN** a workspace where execution would modify files or state
- **WHEN** `EvaluateAsync` is called with an Executor-bound intent
- **THEN** result includes `RequiresConfirmation: true`
- **AND** result includes `Reasoning` that references file scope and safety considerations

#### Scenario: Gating allows non-destructive operations without confirmation

- **GIVEN** a workspace where the next step is read-only verification
- **WHEN** `EvaluateAsync` is called
- **THEN** result includes `RequiresConfirmation: false`
- **AND** result still includes full `Reasoning` and `ProposedAction` for transparency

### Requirement: ProposedAction structured output with schema validation

The system SHALL define a `ProposedAction` schema that the gating engine uses to describe intended operations. This schema MUST be validated before execution.

Required fields:
- `type`: action type discriminator (e.g., "CreatePhasePlan", "ExecuteTasks", "VerifyPhase")
- `description`: human-readable summary of the proposed action
- `phaseId`: optional target phase identifier
- `scope`: optional file scope or resource identifiers affected
- `estimatedImpact`: optional description of side effects

#### Scenario: Valid ProposedAction passes schema validation

- **GIVEN** a gating result with `ProposedAction` containing all required fields
- **WHEN** schema validation is performed
- **THEN** validation succeeds
- **AND** the action can proceed to confirmation step

#### Scenario: Invalid ProposedAction fails with diagnostic error

- **GIVEN** a gating result with `ProposedAction` missing required `type` field
- **WHEN** schema validation is performed
- **THEN** validation fails with clear error indicating missing field
- **AND** the orchestrator emits an error event before attempting execution
