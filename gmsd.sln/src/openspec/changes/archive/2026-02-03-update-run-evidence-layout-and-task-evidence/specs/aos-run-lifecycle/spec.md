## MODIFIED Requirements
### Requirement: Run evidence folder is created deterministically
When a run is started, the system SHALL create a deterministic evidence folder structure under `.aos/evidence/runs/<run-id>/`.

The canonical run evidence folder MUST include the following entries:
- `.aos/evidence/runs/<run-id>/commands.json`
- `.aos/evidence/runs/<run-id>/summary.json`
- `.aos/evidence/runs/<run-id>/logs/`
- `.aos/evidence/runs/<run-id>/artifacts/`
- `.aos/evidence/runs/<run-id>/outputs/`

During the transition period, the system SHOULD continue to accept legacy runs that use the previous layout created by earlier milestones (e.g. `run.json`, `packet.json`, `outputs/` at the run root), but new runs SHOULD be written using the canonical layout above.

#### Scenario: Run start creates the evidence folder structure
- **WHEN** `aos run start` is executed in a repository with an initialized `.aos/` workspace
- **THEN** `.aos/evidence/runs/<run-id>/` exists with the required entries present

### Requirement: Run metadata is written as deterministic JSON
The system SHALL write run metadata to `.aos/evidence/runs/<run-id>/artifacts/run.json`.

During the transition period, the system SHOULD continue to tolerate legacy run metadata written at `.aos/evidence/runs/<run-id>/run.json` for runs created before the canonical PH-ENG-0006 layout.

Run metadata JSON MUST be written using the canonical deterministic JSON writer defined by `aos-deterministic-json-serialization`, including:
- UTF-8 (without BOM)
- LF (`\n`) line endings
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
- **THEN** `.aos/evidence/runs/<run-id>/artifacts/run.json` exists and contains valid JSON

## ADDED Requirements
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

## MODIFIED Requirements
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

