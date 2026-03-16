## ADDED Requirements
### Requirement: Runtime state readiness bootstrap is deterministic
The state layer SHALL provide a deterministic readiness operation to ensure baseline runtime artifacts exist before orchestrator execution.

The readiness operation MUST:
- ensure `.aos/state/state.json` exists and is valid deterministic JSON
- ensure `.aos/state/events.ndjson` exists (empty file permitted)
- perform no writes when artifacts are already valid and present

#### Scenario: Missing state snapshot is created with deterministic baseline content
- **GIVEN** `.aos/state/events.ndjson` exists and is parseable
- **AND** `.aos/state/state.json` is missing
- **WHEN** runtime state readiness bootstrap executes
- **THEN** `state.json` is created at the canonical path
- **AND** `state.json` is deterministic JSON with schema version `1`
- **AND** repeated bootstrap runs with unchanged inputs produce byte-identical snapshot content

#### Scenario: Missing events log is created as an empty append-ready file
- **GIVEN** `.aos/state/state.json` exists and is valid deterministic JSON
- **AND** `.aos/state/events.ndjson` is missing
- **WHEN** runtime state readiness bootstrap executes
- **THEN** `events.ndjson` is created at the canonical path
- **AND** the created log is empty or append-ready NDJSON without invalid placeholder lines

#### Scenario: Readiness bootstrap is idempotent on healthy state
- **GIVEN** `.aos/state/state.json` and `.aos/state/events.ndjson` both exist and are valid
- **WHEN** runtime state readiness bootstrap executes repeatedly
- **THEN** no artifact bytes change across repeated executions

### Requirement: Snapshot can be deterministically re-derived during readiness
When runtime readiness detects a missing or stale `.aos/state/state.json`, the state layer SHALL support deterministic re-derivation from ordered `.aos/state/events.ndjson`.

A re-derived snapshot MUST:
- be produced by replaying events in file order
- be serialized with canonical deterministic JSON rules
- avoid wall-clock or machine-local nondeterministic fields

#### Scenario: Missing snapshot is rebuilt from existing event log
- **GIVEN** `.aos/state/events.ndjson` contains valid ordered events
- **AND** `.aos/state/state.json` is missing
- **WHEN** runtime readiness executes with derive-from-events enabled
- **THEN** `state.json` is created from deterministic event replay
- **AND** repeated derivation from the same event log yields byte-identical `state.json`

#### Scenario: Stale snapshot is deterministically re-derived from events
- **GIVEN** `.aos/state/events.ndjson` contains valid ordered events
- **AND** `.aos/state/state.json` exists but does not match deterministic replay output for the same event stream
- **WHEN** runtime readiness executes with derive-from-events enabled
- **THEN** `state.json` is replaced with the deterministic replay result
- **AND** repeated readiness runs with unchanged events keep `state.json` byte-identical

#### Scenario: Derivation failure returns actionable diagnostics
- **GIVEN** `.aos/state/events.ndjson` contains an invalid non-empty line
- **WHEN** runtime readiness attempts to derive `state.json`
- **THEN** readiness fails with a diagnostic that identifies the invalid event source
- **AND** state artifacts are not partially overwritten
