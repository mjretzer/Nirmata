## ADDED Requirements
### Requirement: Checkpoints can snapshot and restore state
The system SHALL support checkpoints under `.aos/state/checkpoints/**` to enable safe rollback and auditability.

Each checkpoint MUST:
- have a stable ID (e.g., `CHK-...`)
- persist checkpoint metadata as deterministic JSON
- include a state snapshot captured from `.aos/state/state.json`

The system SHALL provide CLI commands:
- `aos checkpoint create`
- `aos checkpoint restore --checkpoint-id <id>`

#### Scenario: Creating a checkpoint snapshots the current state
- **GIVEN** an initialized AOS workspace with a valid `.aos/state/state.json`
- **WHEN** `aos checkpoint create` is executed
- **THEN** a new checkpoint folder exists under `.aos/state/checkpoints/**` containing a deterministic state snapshot

#### Scenario: Restoring a checkpoint rolls back state and records an event
- **GIVEN** an existing checkpoint under `.aos/state/checkpoints/**`
- **WHEN** `aos checkpoint restore --checkpoint-id <id>` is executed
- **THEN** `.aos/state/state.json` matches the checkpoint snapshot and a new state event describing the restore is appended to `.aos/state/events.ndjson`

