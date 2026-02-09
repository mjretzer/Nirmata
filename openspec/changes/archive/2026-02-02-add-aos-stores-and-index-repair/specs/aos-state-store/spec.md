## ADDED Requirements
### Requirement: State snapshot artifact exists
The system SHALL persist a state snapshot at `.aos/state/state.json`.

The state snapshot JSON MUST be written using the canonical deterministic JSON writer defined by `aos-deterministic-json-serialization`.

The state snapshot MUST include at least:
- `schemaVersion` (integer; currently `1`)
- `cursor` (object) describing the current workflow cursor

#### Scenario: State snapshot is created as deterministic JSON
- **WHEN** the state store writes a state snapshot
- **THEN** `.aos/state/state.json` exists and is deterministic JSON

### Requirement: Append-only state event log exists
The system SHALL maintain an append-only event log at `.aos/state/events.ndjson`.

The event log MUST be newline-delimited JSON (NDJSON):
- each non-empty line MUST be a valid JSON object
- new entries MUST be appended to the end of the file
- the file MUST use LF (`\n`) line endings

#### Scenario: Event log appends a new event without rewriting history
- **GIVEN** an existing `.aos/state/events.ndjson` with one or more events
- **WHEN** the state store appends a new event
- **THEN** the new event is appended as a new line and prior lines remain unchanged

