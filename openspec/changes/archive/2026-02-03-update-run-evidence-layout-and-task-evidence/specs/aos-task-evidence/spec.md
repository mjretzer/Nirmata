## ADDED Requirements
### Requirement: Task-evidence latest pointer exists
The system SHALL maintain a task-evidence “latest” pointer for each task at:
`.aos/evidence/task-evidence/<task-id>/latest.json`.

`<task-id>` MUST be a canonical task ID in the format `TSK-######`.

The latest pointer JSON MUST be written using the canonical deterministic JSON writer defined by `aos-deterministic-json-serialization`, including atomic write semantics (no partial/corrupt artifacts) and no-churn semantics when canonical bytes are unchanged.

The latest pointer MUST include at least:
- `schemaVersion` (integer)
- `taskId` (string; MUST match `<task-id>`)
- `runId` (string; a canonical RUN id)
- `gitCommit` (string or null; commit hash slot)
- `diffstat` (object; diffstat slot)

`diffstat` MUST include at least:
- `filesChanged` (integer)
- `insertions` (integer)
- `deletions` (integer)

#### Scenario: Latest pointer is updated atomically on task completion
- **GIVEN** an existing task ID `<task-id>`
- **WHEN** the engine records task evidence completion for `<task-id>`
- **THEN** `.aos/evidence/task-evidence/<task-id>/latest.json` is updated atomically and contains valid deterministic JSON

#### Scenario: Latest pointer remains schema-valid after repeated updates
- **GIVEN** an existing task ID `<task-id>` with an existing `.aos/evidence/task-evidence/<task-id>/latest.json`
- **WHEN** the engine updates the latest pointer multiple times
- **THEN** the latest pointer remains schema-valid and readable as valid JSON after each update

