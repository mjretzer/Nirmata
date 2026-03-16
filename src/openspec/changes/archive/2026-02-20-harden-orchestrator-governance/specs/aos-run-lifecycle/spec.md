# aos-run-lifecycle Specification (Delta)

## ADDED Requirements

### Requirement: Run pause and resume CLI commands exist
The system SHALL provide CLI commands to pause and resume run execution:
- `aos run pause --run-id <id>`
- `aos run resume --run-id <id>`

#### Scenario: Run pause command is available
- **WHEN** `aos run pause --run-id <id>` is executed for a running run
- **THEN** the command succeeds (or fails with an actionable error)

#### Scenario: Run resume command is available
- **WHEN** `aos run resume --run-id <id>` is executed for a paused run
- **THEN** the command succeeds (or fails with an actionable error)

### Requirement: Run metadata includes pause/resume and abandoned status fields
The system SHALL expand the `status` field in run metadata to include pause/resume and abandoned states.

Run metadata JSON MUST include:
- `status` (string; one of: `started`, `paused`, `resumed`, `finished`, `abandoned`)
- `pausedAtUtc` (string; UTC timestamp OR null if not paused)
- `resumedAtUtc` (string; UTC timestamp OR null if not resumed)
- `abandonedAtUtc` (string; UTC timestamp OR null if not abandoned)

#### Scenario: Run metadata reflects paused status
- **GIVEN** a run with status `started`
- **WHEN** `aos run pause --run-id <id>` is executed
- **THEN** run metadata is updated with `"status": "paused"`
- **AND** `pausedAtUtc` is set to current UTC time

#### Scenario: Run metadata reflects resumed status
- **GIVEN** a run with status `paused`
- **WHEN** `aos run resume --run-id <id>` is executed
- **THEN** run metadata is updated with `"status": "resumed"`
- **AND** `resumedAtUtc` is set to current UTC time

#### Scenario: Run metadata reflects abandoned status
- **GIVEN** a run with status `started` older than abandonment timeout
- **WHEN** abandonment detection runs
- **THEN** run metadata is updated with `"status": "abandoned"`
- **AND** `abandonedAtUtc` is set to detection time

### Requirement: Run index includes pause/resume and abandoned status fields
The system SHALL expand the run index to include pause/resume and abandoned status information.

Each `items[]` entry MUST include:
- `status` (string; one of: `started`, `paused`, `resumed`, `finished`, `abandoned`)
- `pausedAtUtc` (string; UTC timestamp OR null)
- `resumedAtUtc` (string; UTC timestamp OR null)
- `abandonedAtUtc` (string; UTC timestamp OR null)

#### Scenario: Run index reflects all status transitions
- **GIVEN** a run that transitions: started → paused → resumed → finished
- **WHEN** each transition occurs
- **THEN** the run index is updated to reflect current status
- **AND** all timestamp fields are accurate

#### Scenario: Run index reflects abandoned status
- **GIVEN** a run marked `abandoned`
- **WHEN** run index is queried
- **THEN** the run's entry shows `"status": "abandoned"`
- **AND** `abandonedAtUtc` is populated

