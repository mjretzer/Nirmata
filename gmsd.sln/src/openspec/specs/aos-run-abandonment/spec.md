# aos-run-abandonment Specification

## Purpose
TBD - created by archiving change harden-orchestrator-governance. Update Purpose after archive.
## Requirements
### Requirement: Run abandonment status
The system SHALL support an `abandoned` status for runs that remain unfinished after a configurable timeout.

A run is abandoned when:
- Its `startedAtUtc` timestamp is older than `abandonmentTimeoutMinutes` (configured in `.aos/config/run-lifecycle.json`)
- Its `status` is not `finished`
- Abandonment detection has been run (either automatically or via command)

#### Scenario: Run marked abandoned after timeout
- **GIVEN** a run was started 25 hours ago with default timeout (1440 minutes)
- **WHEN** abandonment detection runs
- **THEN** the run's status is updated to `abandoned` in `.aos/evidence/runs/<run-id>/run.json`
- **AND** the run index at `.aos/evidence/runs/index.json` reflects the `abandoned` status

#### Scenario: Run not marked abandoned before timeout
- **GIVEN** a run was started 1 hour ago with default timeout (1440 minutes)
- **WHEN** abandonment detection runs
- **THEN** the run's status remains unchanged (not marked `abandoned`)

#### Scenario: Finished run is never marked abandoned
- **GIVEN** a run with status `finished`
- **WHEN** abandonment detection runs
- **THEN** the run's status remains `finished` (not changed to `abandoned`)

### Requirement: Abandonment timeout configuration
The system SHALL read abandonment timeout from `.aos/config/run-lifecycle.json`.

The configuration file MUST include:
- `abandonmentTimeoutMinutes` (integer; default 1440 = 24 hours)

The timeout MUST be:
- Positive integer (>= 1)
- Applied consistently across all abandonment detection operations
- Configurable without code changes

#### Scenario: Default timeout is 24 hours
- **GIVEN** `.aos/config/run-lifecycle.json` does not specify `abandonmentTimeoutMinutes`
- **WHEN** abandonment detection runs
- **THEN** the default timeout of 1440 minutes (24 hours) is used

#### Scenario: Custom timeout is respected
- **GIVEN** `.aos/config/run-lifecycle.json` specifies `abandonmentTimeoutMinutes: 60`
- **WHEN** abandonment detection runs
- **THEN** runs older than 60 minutes (not finished) are marked `abandoned`

#### Scenario: Invalid timeout is rejected
- **GIVEN** `.aos/config/run-lifecycle.json` specifies `abandonmentTimeoutMinutes: 0`
- **WHEN** abandonment detection runs
- **THEN** an error is raised indicating timeout must be >= 1

### Requirement: Abandonment detection operation
The system SHALL provide an operation to detect and mark abandoned runs.

The operation MUST:
- Scan all runs in `.aos/evidence/runs/`
- Compare `startedAtUtc` against current time and configured timeout
- Mark unfinished runs as `abandoned` if timeout exceeded
- Update run metadata and run index atomically
- Return a summary of marked runs

#### Scenario: Abandonment detection marks multiple runs
- **GIVEN** three unfinished runs older than timeout
- **WHEN** abandonment detection runs
- **THEN** all three runs are marked `abandoned`
- **AND** the run index reflects all three as `abandoned`

#### Scenario: Abandonment detection is idempotent
- **GIVEN** a run already marked `abandoned`
- **WHEN** abandonment detection runs again
- **THEN** the run remains `abandoned` (no duplicate marking)

### Requirement: Manual abandonment cleanup command
The system SHALL provide `aos cache prune --abandoned` to manually clean abandoned runs.

The command MUST:
- Accept optional `--run-id <id>` to clean a specific run
- If no run ID specified, clean all abandoned runs
- Move evidence to `.aos/cache/abandoned/<run-id>/` (non-authoritative storage)
- Update run index to remove cleaned runs
- Return summary of cleaned runs

#### Scenario: Prune all abandoned runs
- **GIVEN** five abandoned runs in `.aos/evidence/runs/`
- **WHEN** `aos cache prune --abandoned` is executed
- **THEN** all five runs are moved to `.aos/cache/abandoned/`
- **AND** the run index no longer lists them
- **AND** the command returns count of cleaned runs

#### Scenario: Prune specific abandoned run
- **GIVEN** an abandoned run with ID `RUN-0042`
- **WHEN** `aos cache prune --abandoned --run-id RUN-0042` is executed
- **THEN** `RUN-0042` is moved to `.aos/cache/abandoned/RUN-0042/`
- **AND** the run index no longer lists `RUN-0042`

#### Scenario: Prune fails on non-abandoned run
- **GIVEN** a run with status `finished`
- **WHEN** `aos cache prune --abandoned --run-id <id>` is executed
- **THEN** the command fails with error indicating run is not abandoned

### Requirement: Background abandonment cleanup task
The system SHALL provide a background task (in `Gmsd.Windows.Service`) to periodically detect and mark abandoned runs.

The task MUST:
- Run at configurable interval (default: every 1 hour)
- Detect abandoned runs using the abandonment detection operation
- Log results (count of marked runs, any errors)
- Not block other service operations
- Be safe to run concurrently with other commands (respects workspace lock)

#### Scenario: Background task marks abandoned runs
- **GIVEN** the Windows Service is running
- **AND** a run has been unfinished for > timeout
- **WHEN** the background task interval elapses
- **THEN** the run is marked `abandoned`
- **AND** the service logs the detection

#### Scenario: Background task respects workspace lock
- **GIVEN** the workspace lock is held by another process
- **WHEN** the background task runs
- **THEN** the task skips detection (does not block; will retry next interval)

### Requirement: Abandoned run recovery guidance
The system SHALL provide clear guidance for recovering from abandoned runs.

When a run is marked `abandoned`, the system MUST:
- Preserve all evidence in `.aos/evidence/runs/<run-id>/`
- Document the abandonment reason (timeout exceeded)
- Suggest recovery steps in error messages and documentation

#### Scenario: Abandoned run evidence is preserved
- **GIVEN** a run marked `abandoned`
- **WHEN** evidence is queried
- **THEN** all task results, logs, and artifacts are still available
- **AND** the run can be manually inspected or resumed

#### Scenario: Documentation guides abandoned run recovery
- **GIVEN** a user encounters an abandoned run
- **WHEN** they consult troubleshooting documentation
- **THEN** they find clear steps to inspect evidence and decide on recovery (resume, retry, or cleanup)

