# prerequisite-validation Specification

## Purpose
Define the behavior for validating workspace prerequisites before workflow execution, converting hard failures into conversational recovery flows where the agent asks the user rather than failing.

## ADDED Requirements

### Requirement: Prerequisite Validation Before Workflow Execution
The system SHALL validate that required workspace artifacts exist before starting workflow phases.

#### Scenario: Project spec is required for Roadmapper
- **GIVEN** the user requests roadmap creation
- **AND** `.aos/spec/project-spec.json` does not exist
- **WHEN** the prerequisite validator checks requirements
- **THEN** it SHALL return `MissingPrerequisite.ProjectSpec`
- **AND** NOT throw an exception

#### Scenario: Roadmap is required for Planner
- **GIVEN** the user requests phase planning
- **AND** `.aos/spec/roadmap.json` does not exist
- **WHEN** the prerequisite validator checks requirements
- **THEN** it SHALL return `MissingPrerequisite.Roadmap`
- **AND** include recovery suggestion

#### Scenario: Phase plan is required for Executor
- **GIVEN** the user requests task execution
- **AND** no plan exists for the current cursor position
- **WHEN** the prerequisite validator checks requirements
- **THEN** it SHALL return `MissingPrerequisite.Plan`
- **AND** identify the missing cursor position

#### Scenario: State file is required for resumable operations
- **GIVEN** the user requests run resumption
- **AND** `.aos/state/state.json` does not exist
- **WHEN** the prerequisite validator checks requirements
- **THEN** it SHALL return `MissingPrerequisite.State`
- **AND** the recovery action SHALL be to start fresh

### Requirement: Conversational Recovery Instead of Hard Failure
The system SHALL convert missing prerequisite failures into conversational assistant responses.

#### Scenario: Missing project spec triggers conversational ask
- **GIVEN** `MissingPrerequisite.ProjectSpec` is detected
- **WHEN** the orchestrator processes the result
- **THEN** it SHALL emit `assistant.final` with message:
  - "I need a project specification before creating a roadmap."
  - "Would you like me to start the project interviewer?"
- **AND** include suggested command `/interview`

#### Scenario: Missing roadmap triggers planning offer
- **GIVEN** `MissingPrerequisite.Roadmap` is detected
- **WHEN** the orchestrator processes the result
- **THEN** it SHALL emit `assistant.final` with message:
  - "I need to create a roadmap before planning phases."
  - "Should I create the roadmap based on the project spec?"
- **AND** include suggested command `/roadmap --create`

#### Scenario: Missing phase plan triggers planning suggestion
- **GIVEN** `MissingPrerequisite.Plan` is detected for cursor "PH-0001"
- **WHEN** the orchestrator processes the result
- **THEN** it SHALL emit `assistant.final` with message:
  - "There's no plan for phase PH-0001 yet."
  - "Would you like me to create a plan for this phase?"
- **AND** include suggested command `/plan --phase PH-0001`

#### Scenario: Multiple missing prerequisites handled sequentially
- **GIVEN** both ProjectSpec and Roadmap are missing
- **WHEN** the validator detects missing prerequisites
- **THEN** it SHALL prioritize ProjectSpec (prerequisite of Roadmap)
- **AND** only ask about Roadmap after ProjectSpec is resolved

### Requirement: Prerequisite Validation Result Structure
The system SHALL return structured prerequisite validation results.

#### Scenario: Validation result includes recovery action
- **GIVEN** any missing prerequisite is detected
- **WHEN** the validation result is constructed
- **THEN** it SHALL include:
  - `IsSatisfied`: false
  - `Missing`: The specific missing prerequisite type
  - `RecoveryAction`: A `ProposedAction` describing how to resolve
  - `SuggestedCommand`: Explicit command to initiate recovery

#### Scenario: All prerequisites satisfied
- **GIVEN** all required workspace artifacts exist
- **WHEN** the prerequisite validator completes checks
- **THEN** it SHALL return `IsSatisfied: true`
- **AND** an empty `Missing` field
- **AND** allow the workflow to proceed

### Requirement: Prerequisite-Aware Gating Context
The system SHALL include prerequisite status in the gating context.

#### Scenario: GatingContext exposes prerequisite flags
- **GIVEN** the `GatingContext` is constructed
- **WHEN** prerequisite validation runs
- **THEN** the context SHALL include:
  - `HasProject`: boolean
  - `HasRoadmap`: boolean
  - `HasPlan`: boolean (for current cursor)
  - `HasState`: boolean

#### Scenario: Prerequisite flags influence gating decisions
- **GIVEN** `HasProject` is false
- **WHEN** the gating engine evaluates routing
- **THEN** it SHALL route to "Interviewer" phase
- **AND** include prerequisite context in reasoning

### Requirement: Dynamic Prerequisite Discovery
The system SHALL discover available prerequisites dynamically rather than hardcoding checks.

#### Scenario: Scan for available project specs
- **GIVEN** the validator needs to check for project spec
- **WHEN** it scans `.aos/spec/`
- **THEN** it SHALL look for `project-spec.json` or any `*-spec.json`
- **AND** report what was found vs. what was expected

#### Scenario: Detect partial workspace initialization
- **GIVEN** some but not all prerequisites exist
- **WHEN** the validator scans the workspace
- **THEN** it SHALL report partial initialization status
- **AND** identify which specific files are missing

#### Scenario: Validate prerequisite file content
- **GIVEN** a prerequisite file exists (e.g., `project-spec.json`)
- **WHEN** the validator checks it
- **THEN** it SHALL validate the JSON is well-formed
- **AND** report "corrupted prerequisite" if invalid

### Requirement: Workspace Bootstrap Detection
The system SHALL detect completely uninitialized workspaces and offer bootstrap.

#### Scenario: Empty workspace detected
- **GIVEN** the `.aos/` directory does not exist or is empty
- **WHEN** any workflow is requested
- **THEN** the validator SHALL return `MissingPrerequisite.Workspace`
- **AND** the recovery action SHALL offer workspace initialization

#### Scenario: Bootstrap offered on first interaction
- **GIVEN** `MissingPrerequisite.Workspace` is detected
- **WHEN** the conversational recovery runs
- **THEN** it SHALL emit:
  - "Welcome! I need to set up a workspace first."
  - "Should I initialize the workspace in this directory?"
- **AND** include suggested command `/init`

### Requirement: GatingEngine Prerequisite Validation Integration
The gating engine SHALL validate workspace prerequisites before making phase routing decisions and SHALL convert missing prerequisites into conversational assistant responses rather than routing to workflow phases.

#### Scenario: Prerequisite check before phase routing
- **GIVEN** the gating engine is evaluating routing to the "Roadmapper" phase
- **WHEN** prerequisite validation runs
- **THEN** it SHALL verify that `.aos/spec/project-spec.json` exists
- **AND** if missing, emit a conversational response instead of routing to Roadmapper

#### Scenario: Missing prerequisite triggers conversational recovery
- **GIVEN** the "Planner" phase is requested
- **AND** the roadmap prerequisite is missing
- **WHEN** the gating engine processes the request
- **THEN** it SHALL emit `assistant.final` with a message asking the user if they want to create the roadmap first
- **AND** include the suggested command `/roadmap --create`
- **AND** NOT proceed to the Planner phase dispatch

#### Scenario: Satisfied prerequisites allow normal routing
- **GIVEN** all prerequisites for the "Executor" phase are satisfied
- **WHEN** the gating engine evaluates routing
- **THEN** it SHALL proceed with normal phase routing logic
- **AND** return the standard `GatingResult` with `TargetPhase = "Executor"`

## Cross-References

- Related to: `confirmation-gate` spec (uses prerequisite validation)
- Related to: `aos-workspace-bootstrap` (workspace initialization)
- Related to: `aos-spec-store` (spec file management)
- Related to: `aos-state-store` (state file management)
- Related to: `agents-interviewer-workflow` (recovery to Interviewer)
