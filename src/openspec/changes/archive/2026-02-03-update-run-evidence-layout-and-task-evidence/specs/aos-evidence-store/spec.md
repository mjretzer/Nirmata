## MODIFIED Requirements
### Requirement: Evidence command log is standardized
The system SHALL maintain a machine-readable command log at `.aos/evidence/logs/commands.json` that records engine/CLI actions taken in the workspace.

The command log JSON MUST be written using the canonical deterministic JSON writer defined by `aos-deterministic-json-serialization`.

The command log MUST have the shape:
- `schemaVersion` (integer; currently `1`)
- `items` (array)

Each `items[]` entry MUST include at least:
- `command` (string; the executed command name)
- `args` (array of strings)
- `exitCode` (integer)
- `runId` (string or null; set when the command is associated to a run)

In addition, the system SHALL maintain a per-run command log view at `.aos/evidence/runs/<run-id>/commands.json` for each run. The per-run command log MUST include only entries whose `runId` matches `<run-id>`.

#### Scenario: A CLI command records an entry in commands.json
- **WHEN** the user executes any CLI command that mutates `.aos/**`
- **THEN** `.aos/evidence/logs/commands.json` is updated to include an entry describing the command

### Requirement: Run manifest records produced artifacts and hashes
The system SHALL write a run manifest at `.aos/evidence/runs/<run-id>/artifacts/manifest.json` for each run.

During the transition period, the system SHOULD continue to tolerate legacy run manifests written at `.aos/evidence/runs/<run-id>/manifest.json` for runs created before the canonical PH-ENG-0006 layout.

The run manifest JSON MUST be written using the canonical deterministic JSON writer defined by `aos-deterministic-json-serialization`.

The run manifest MUST include at least:
- `schemaVersion` (integer; currently `1`)
- `runId` (string; MUST match `<run-id>`)
- `outputs` (array)

Each `outputs[]` entry MUST include at least:
- `relativePath` (string; relative to `.aos/evidence/runs/<run-id>/outputs/`)
- `sha256` (string; lower-case hex)

`outputs` MUST be sorted by `relativePath` using ordinal string ordering.

#### Scenario: Execute-plan produces a run manifest including output hashes
- **GIVEN** a run with one or more output files written under `.aos/evidence/runs/<run-id>/outputs/**`
- **WHEN** the run is finished
- **THEN** `.aos/evidence/runs/<run-id>/artifacts/manifest.json` exists and enumerates each output with its SHA-256 hash

