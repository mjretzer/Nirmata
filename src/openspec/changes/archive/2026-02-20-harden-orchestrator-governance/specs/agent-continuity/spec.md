# agent-continuity Specification (Delta)

## ADDED Requirements

### Requirement: Pause/resume workflow with explicit state transitions
The system SHALL support explicit pause/resume state transitions for long-running workflows.

Pause/resume behavior MUST:
- Be triggered by explicit commands (`aos run pause`, `aos run resume`)
- Update run state in `.aos/state/run.json` with status transitions
- Preserve execution context (cursor, task evidence, logs) during pause
- Allow resumption from exact pause point
- Be visible to user via `report-progress` and UI

#### Scenario: Run is paused explicitly
- **GIVEN** a running workflow
- **WHEN** `aos run pause --run-id <id>` is executed
- **THEN** the workflow stops accepting new task executions
- **AND** run state is updated with `status: paused` and `pausedAtUtc`
- **AND** execution context is preserved

#### Scenario: Run is resumed from pause point
- **GIVEN** a paused workflow
- **WHEN** `aos run resume --run-id <id>` is executed
- **THEN** the workflow resumes from the exact pause point
- **AND** run state is updated with `status: resumed` and `resumedAtUtc`
- **AND** execution context is intact (cursor, evidence, logs)

#### Scenario: Pause is idempotent
- **GIVEN** a paused workflow
- **WHEN** `aos run pause --run-id <id>` is executed again
- **THEN** the command succeeds (no error)
- **AND** status remains `paused`

#### Scenario: Resume requires paused status
- **GIVEN** a running workflow (status `started`)
- **WHEN** `aos run resume --run-id <id>` is executed
- **THEN** the command fails with error indicating run is not paused

### Requirement: Progress reporting includes run status
The system SHALL include run status in progress reports.

Progress reports MUST include:
- Current run status (started/paused/resumed/finished/abandoned)
- Time in current status
- Pause/resume history (if applicable)
- Estimated time to completion (if available)

#### Scenario: Progress report shows paused status
- **GIVEN** a paused workflow
- **WHEN** `report-progress` is invoked
- **THEN** output includes `"status": "paused"`
- **AND** includes `"pausedAtUtc": "2026-02-20T10:30:00Z"`
- **AND** includes time paused (e.g., "5 minutes")

#### Scenario: Progress report shows resumed status
- **GIVEN** a resumed workflow
- **WHEN** `report-progress` is invoked
- **THEN** output includes `"status": "resumed"`
- **AND** includes `"resumedAtUtc": "2026-02-20T10:35:00Z"`

#### Scenario: Progress report shows abandoned status
- **GIVEN** a workflow marked `abandoned`
- **WHEN** `report-progress` is invoked
- **THEN** output includes `"status": "abandoned"`
- **AND** includes `"abandonedAtUtc"` timestamp
- **AND** suggests recovery steps

### Requirement: UI displays pause/resume controls and status
The system SHALL display pause/resume controls and status in the orchestrator UI.

UI MUST:
- Show current run status prominently (started/paused/resumed/finished/abandoned)
- Provide pause button when run is active (started/resumed)
- Provide resume button when run is paused
- Display status timeline (started → paused → resumed → finished)
- Show pause/resume timestamps
- Provide clear recovery guidance for abandoned runs

#### Scenario: UI shows pause button for active run
- **GIVEN** a running workflow
- **WHEN** the orchestrator page is viewed
- **THEN** a "Pause" button is visible and enabled
- **AND** current status is displayed as "Running"

#### Scenario: UI shows resume button for paused run
- **GIVEN** a paused workflow
- **WHEN** the orchestrator page is viewed
- **THEN** a "Resume" button is visible and enabled
- **AND** current status is displayed as "Paused"
- **AND** pause timestamp is shown

#### Scenario: UI shows abandoned run guidance
- **GIVEN** a workflow marked `abandoned`
- **WHEN** the orchestrator page is viewed
- **THEN** status is displayed as "Abandoned"
- **AND** abandonment reason is shown (timeout exceeded)
- **AND** recovery options are suggested (inspect evidence, retry, cleanup)

### Requirement: Continuity plane respects pause/resume state
The system SHALL ensure the continuity plane respects pause/resume state transitions.

Behavior MUST:
- Not execute new tasks while paused
- Resume execution from cursor on resume command
- Preserve all state (cursor, evidence, logs) during pause
- Log pause/resume events for audit trail

#### Scenario: Task executor respects paused state
- **GIVEN** a workflow is paused
- **WHEN** task executor checks for next task
- **THEN** no new tasks are dequeued or executed
- **AND** executor waits for resume command

#### Scenario: Cursor is preserved during pause
- **GIVEN** a workflow paused at task TSK-0005
- **WHEN** workflow is resumed
- **THEN** cursor remains at TSK-0005
- **AND** next task to execute is TSK-0006

#### Scenario: Pause/resume is logged
- **GIVEN** a workflow transitions: started → paused → resumed
- **WHEN** transitions occur
- **THEN** service logs include entries for each transition
- **AND** logs include timestamps and run ID
