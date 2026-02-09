## ADDED Requirements

### Requirement: Handoff state capture
The system SHALL provide the ability to capture a complete handoff snapshot during workflow execution that preserves all state necessary for deterministic resumption.

The handoff state SHALL include:
- Current cursor position (phase, milestone, task IDs)
- In-flight task context (task ID, partial results, execution packet)
- Scope constraints (file boundaries, edit restrictions)
- Next pending command with arguments
- Source run ID for provenance tracking

#### Scenario: Pause captures complete execution context
- **GIVEN** an active workflow execution with task TSK-0001 in progress
- **WHEN** `PauseResumeManager.PauseAsync()` is invoked
- **THEN** `.aos/state/handoff.json` is created with cursor, task context, scope, and next command
- **AND** the file uses deterministic JSON serialization (stable key ordering, LF endings)

#### Scenario: Handoff includes scope preservation
- **GIVEN** an execution with scope constraints (e.g., file whitelist: ["src/utils.ts"])
- **WHEN** pause is triggered
- **THEN** handoff.json contains the exact scope constraints
- **AND** resumed execution respects the same scope boundaries

### Requirement: pause-work command
The system SHALL provide a `pause-work` command that creates an interruption-safe handoff snapshot.

The command SHALL:
- Accept optional reason/message for the pause
- Capture current cursor position from `IStateStore`
- Capture in-flight task context from active run
- Write handoff.json to `.aos/state/handoff.json`
- Return handoff metadata including timestamp and captured run ID

#### Scenario: pause-work during active task
- **GIVEN** workflow is executing task TSK-0001 in phase "Implementation"
- **WHEN** `pause-work` command is invoked with reason "user interruption"
- **THEN** handoff.json captures task ID, phase cursor, and scope
- **AND** handoff.json contains `"reason": "user interruption"`

#### Scenario: pause-work with no active run
- **GIVEN** no active workflow execution
- **WHEN** `pause-work` command is invoked
- **THEN** return error indicating no active execution to pause
- **AND** do not create or modify handoff.json

### Requirement: resume-work command
The system SHALL provide a `resume-work` command that reconstructs execution state from the handoff snapshot.

The command SHALL:
- Read `.aos/state/handoff.json`
- Validate handoff schema and freshness (optional TTL check)
- Restore cursor position to `IStateStore`
- Reconstruct execution packet from handoff context
- Resume workflow from the captured next command
- Return resumed run ID and status

#### Scenario: resume-work reconstructs deterministic continuation
- **GIVEN** a valid `.aos/state/handoff.json` from a paused task TSK-0001
- **WHEN** `resume-work` command is invoked
- **THEN** cursor is restored to the saved position
- **AND** the next action is the captured command from handoff
- **AND** resumed task respects the same scope constraints as the original

#### Scenario: resume-work with missing handoff
- **GIVEN** no `.aos/state/handoff.json` exists
- **WHEN** `resume-work` command is invoked
- **THEN** return error indicating no handoff available to resume from
- **AND** suggest creating a new run instead

### Requirement: resume-task by RUN ID
The system SHALL provide a `resume-task` command that can restore execution state from any historical RUN evidence folder.

The command SHALL:
- Accept RUN ID as argument (e.g., `resume-task --run-id RUN-a1b2c3d4`)
- Locate evidence folder at `.aos/evidence/runs/<run-id>/`
- Read `summary.json` and `packet.json` to reconstruct execution context
- Restore applicable state (cursor, scope, task context)
- Start a new continuation run linked to the source run ID

#### Scenario: resume-task locates historical evidence
- **GIVEN** an archived run at `.aos/evidence/runs/RUN-a1b2c3d4/` with task evidence
- **WHEN** `resume-task --run-id RUN-a1b2c3d4` is invoked
- **THEN** the run folder is located and summary.json is parsed
- **AND** execution packet is reconstructed from artifacts/packet.json
- **AND** a new run is started with provenance link to RUN-a1b2c3d4

#### Scenario: resume-task with non-existent RUN ID
- **GIVEN** no evidence folder exists for RUN-xyz123
- **WHEN** `resume-task --run-id RUN-xyz123` is invoked
- **THEN** return error indicating run not found
- **AND** list available runs from `.aos/evidence/runs/index.json`

### Requirement: Deterministic resume semantics
The system SHALL guarantee that resumed execution produces deterministic continuation from the pause point.

Resumed execution SHALL:
- Begin with the exact next command captured at pause time
- Operate on identical scope constraints as original execution
- Produce equivalent outputs (modulo timestamps, run IDs) given same inputs
- Not repeat already-completed work from the original run

#### Scenario: Resume is idempotent to original execution
- **GIVEN** a workflow paused at command C after completing commands A and B
- **WHEN** resumed and allowed to complete
- **THEN** final state matches what would have occurred without pause
- **AND** commands A and B effects are not duplicated

### Requirement: Handoff JSON schema validation
The system SHALL validate handoff.json against a defined schema with embedded schema version.

The schema SHALL include:
- `schemaVersion` field (current: "1.0")
- `timestamp` ISO8601 timestamp of pause
- `sourceRunId` provenance tracking
- `cursor` object with phase, milestone, task IDs
- `taskContext` with task ID, status, partial results
- `scope` object with constraints
- `nextCommand` object with name and arguments

#### Scenario: Handoff passes schema validation
- **GIVEN** a handoff.json generated by PauseResumeManager
- **WHEN** schema validator processes the file
- **THEN** all required fields are present with valid types
- **AND** schemaVersion matches supported versions

#### Scenario: Invalid handoff fails validation
- **GIVEN** a handoff.json with missing required fields (e.g., no cursor)
- **WHEN** schema validator processes the file
- **THEN** validation errors are returned listing missing/invalid fields
- **AND** resume operation is blocked with clear error message
