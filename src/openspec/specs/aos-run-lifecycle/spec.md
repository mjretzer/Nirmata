# aos-run-lifecycle Specification

## Purpose

Defines canonical AOS workspace contracts and behavioral semantics for $capabilityId.

- **Lives in:** `nirmata.Aos/*`, `.aos/**`
- **Owns:** Engine-level artifact contracts, validation, and deterministic IO semantics for this capability
- **Does not own:** Plane/orchestrator workflows (owned by `agents-*` capabilities)
## Requirements
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
- `.aos/evidence/runs/<run-id>/packet.json`
- `.aos/evidence/runs/<run-id>/logs/`
- `.aos/evidence/runs/<run-id>/outputs/`

#### Scenario: Run start creates the evidence folder structure
- **WHEN** `aos run start` is executed in a repository with an initialized `.aos/` workspace
- **THEN** `.aos/evidence/runs/<run-id>/` exists with the required entries present

### Requirement: Run metadata is written as deterministic JSON
The system SHALL write run metadata to `.aos/evidence/runs/<run-id>/run.json`.

Run metadata JSON MUST be written using the canonical deterministic JSON writer defined by `aos-deterministic-json-serialization`, including:
- UTF-8 (without BOM)
- LF (`\\n`) line endings
- valid JSON
- stable recursive key ordering and stable formatting (so fixture comparison is possible)
- atomic write semantics (no partial/corrupt artifacts)
- no-churn semantics when canonical bytes are unchanged

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

The run index JSON MUST be written using the canonical deterministic JSON writer defined by `aos-deterministic-json-serialization`, including:
- UTF-8 (without BOM)
- LF (`\\n`) line endings
- valid JSON
- stable recursive key ordering and stable formatting (so fixture comparison is possible)
- atomic write semantics (no partial/corrupt artifacts)
- no-churn semantics when canonical bytes are unchanged

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

### Requirement: Run packet artifact is written as deterministic JSON
When a run is started, the system SHALL write a packet artifact to:
`.aos/evidence/runs/<run-id>/artifacts/packet.json`.

During the transition period, the system SHOULD continue to tolerate legacy run packets written at `.aos/evidence/runs/<run-id>/packet.json` for runs created before the canonical PH-ENG-0006 layout.

The packet JSON MUST be written using the canonical deterministic JSON writer defined by `aos-deterministic-json-serialization`.

The packet MUST include at least:
- `schemaVersion` (integer)
- `runId` (string; MUST match `<run-id>`)
- `command` (string; normalized command name, e.g. `execute-plan`)
- `args` (array of strings; raw CLI args as received by the command handler)

#### Scenario: Run packet is created at run start
- **WHEN** `aos run start` is executed
- **THEN** `.aos/evidence/runs/<run-id>/artifacts/packet.json` exists and contains valid deterministic JSON

### Requirement: Run result artifact is written as deterministic JSON
When a run is finished, the system SHALL write a result artifact to:
`.aos/evidence/runs/<run-id>/artifacts/result.json`.

During the transition period, the system SHOULD continue to tolerate legacy run results written at `.aos/evidence/runs/<run-id>/result.json` for runs created before the canonical PH-ENG-0006 layout.

The result JSON MUST be written using the canonical deterministic JSON writer defined by `aos-deterministic-json-serialization`.

The result MUST include at least:
- `schemaVersion` (integer)
- `runId` (string; MUST match `<run-id>`)
- `status` (string; e.g., `succeeded` or `failed`)
- `exitCode` (integer; stable CLI exit code)

#### Scenario: Run result is created at run finish
- **GIVEN** an existing run created by `aos run start`
- **WHEN** `aos run finish --run-id <run-id>` is executed
- **THEN** `.aos/evidence/runs/<run-id>/artifacts/result.json` exists and contains valid deterministic JSON

### Requirement: Run summary is written deterministically
The system SHALL write a run summary artifact to `.aos/evidence/runs/<run-id>/summary.json`.

The run summary JSON MUST be written using the canonical deterministic JSON writer defined by `aos-deterministic-json-serialization`, including atomic write semantics and no-churn semantics when canonical bytes are unchanged.

The run summary MUST include at least:
- `schemaVersion` (integer)
- `runId` (string; MUST match `<run-id>`)
- `status` (string)
- `startedAtUtc` (string; UTC timestamp)
- `finishedAtUtc` (string or null; UTC timestamp OR null)
- `exitCode` (integer)
- `artifacts` (object; pointers to key per-run artifacts by contract path)

#### Scenario: Run summary exists after run finish
- **GIVEN** an existing run created by `aos run start`
- **WHEN** `aos run finish --run-id <run-id>` is executed
- **THEN** `.aos/evidence/runs/<run-id>/summary.json` exists and contains valid deterministic JSON

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
- **GIVEN** a run that transitions: started ? paused ? resumed ? finished
- **WHEN** each transition occurs
- **THEN** the run index is updated to reflect current status
- **AND** all timestamp fields are accurate

#### Scenario: Run index reflects abandoned status
- **GIVEN** a run marked `abandoned`
- **WHEN** run index is queried
- **THEN** the run's entry shows `"status": "abandoned"`
- **AND** `abandonedAtUtc` is populated

