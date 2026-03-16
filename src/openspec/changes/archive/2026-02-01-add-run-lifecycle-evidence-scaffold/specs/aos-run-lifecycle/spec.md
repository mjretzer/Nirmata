## ADDED Requirements

### Requirement: Run lifecycle CLI commands exist
The system SHALL provide CLI commands to manage a minimal run lifecycle:
- `aos run start`
- `aos run finish`

#### Scenario: Run start command is available
- **WHEN** `aos run start` is executed
- **THEN** the command succeeds (or fails with an actionable error) and produces a run ID

#### Scenario: Run finish command is available
- **WHEN** `aos run finish` is executed for an existing run
- **THEN** the command succeeds (or fails with an actionable error)

### Requirement: Run finish supports explicit run ID selection
The system SHALL allow selecting the run to finish explicitly via `--run-id <run-id>`.

#### Scenario: Run finish accepts --run-id
- **GIVEN** an existing run created by `aos run start`
- **WHEN** `aos run finish --run-id <run-id>` is executed
- **THEN** the command succeeds (or fails with an actionable error)

### Requirement: Run evidence folder is created deterministically
When a run is started, the system SHALL create a deterministic evidence folder structure under `.aos/evidence/runs/<run-id>/`.

The evidence folder MUST include the following entries:
- `.aos/evidence/runs/<run-id>/run.json`
- `.aos/evidence/runs/<run-id>/logs/`
- `.aos/evidence/runs/<run-id>/outputs/`

#### Scenario: Run start creates the evidence folder structure
- **WHEN** `aos run start` is executed in a repository with an initialized `.aos/` workspace
- **THEN** `.aos/evidence/runs/<run-id>/` exists with the required entries present

### Requirement: Run metadata is written as deterministic JSON
The system SHALL write run metadata to `.aos/evidence/runs/<run-id>/run.json`.

Run metadata JSON MUST be:
- UTF-8 (without BOM)
- LF (`\\n`) line endings
- valid JSON
- written with stable key ordering and stable formatting (so fixture comparison is possible)

The run metadata JSON MUST include at least:
- `schemaVersion` (integer)
- `runId` (string; MUST match `<run-id>`)
- `status` (string; at minimum: `started` or `finished`)
- `startedAtUtc` (string; UTC timestamp)
- `finishedAtUtc` (string; UTC timestamp OR null if not finished)

#### Scenario: Run metadata file is created
- **WHEN** `aos run start` is executed
- **THEN** `.aos/evidence/runs/<run-id>/run.json` exists and contains valid JSON

### Requirement: Run index is maintained under evidence
The system SHALL maintain a deterministic run index at `.aos/evidence/runs/index.json` so tooling can enumerate runs without scanning the filesystem.

The run index JSON MUST be:
- UTF-8 (without BOM)
- LF (`\\n`) line endings
- valid JSON
- written with stable key ordering and stable formatting (so fixture comparison is possible)

The run index JSON MUST have the shape:
- `schemaVersion` (integer)
- `items` (array)

Each `items[]` entry MUST include at least:
- `runId` (string)
- `status` (string; at minimum: `started` or `finished`)
- `startedAtUtc` (string; UTC timestamp)
- `finishedAtUtc` (string; UTC timestamp OR null)

#### Scenario: Run start adds an entry to the run index
- **WHEN** `aos run start` is executed
- **THEN** `.aos/evidence/runs/index.json` exists and includes the new run ID

#### Scenario: Run finish updates run metadata and index
- **GIVEN** an existing run created by `aos run start`
- **WHEN** `aos run finish` is executed for that run
- **THEN** the run’s metadata and the run index reflect that the run is finished

