## MODIFIED Requirements
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

## ADDED Requirements
### Requirement: Run packet artifact is written as deterministic JSON
When a run is started, the system SHALL write a packet artifact to:
`.aos/evidence/runs/<run-id>/packet.json`.

The packet JSON MUST be written using the canonical deterministic JSON writer defined by `aos-deterministic-json-serialization`.

The packet MUST include at least:
- `schemaVersion` (integer)
- `runId` (string; MUST match `<run-id>`)
- `command` (string; normalized command name, e.g. `execute-plan`)
- `args` (array of strings; raw CLI args as received by the command handler)

#### Scenario: Run packet is created at run start
- **WHEN** `aos run start` is executed
- **THEN** `.aos/evidence/runs/<run-id>/packet.json` exists and contains valid deterministic JSON

### Requirement: Run result artifact is written as deterministic JSON
When a run is finished, the system SHALL write a result artifact to:
`.aos/evidence/runs/<run-id>/result.json`.

The result JSON MUST be written using the canonical deterministic JSON writer defined by `aos-deterministic-json-serialization`.

The result MUST include at least:
- `schemaVersion` (integer)
- `runId` (string; MUST match `<run-id>`)
- `status` (string; e.g., `succeeded` or `failed`)
- `exitCode` (integer; stable CLI exit code)

#### Scenario: Run result is created at run finish
- **GIVEN** an existing run created by `aos run start`
- **WHEN** `aos run finish --run-id <run-id>` is executed
- **THEN** `.aos/evidence/runs/<run-id>/result.json` exists and contains valid deterministic JSON

