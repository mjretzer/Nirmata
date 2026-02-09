## MODIFIED Requirements
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
