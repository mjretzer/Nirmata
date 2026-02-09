## MODIFIED Requirements

### Requirement: State snapshot artifact exists
The system SHALL persist a state snapshot at `.aos/state/state.json`.

The state snapshot JSON MUST be written using the canonical deterministic JSON writer defined by `aos-deterministic-json-serialization`.

The state snapshot MUST include at least:
- `schemaVersion` (integer; currently `1`)
- `cursor` (object) describing the current workflow cursor

The cursor object MAY include a minimal reference shape:
- `cursor.kind` (string; recognized artifact kind)
- `cursor.id` (string; artifact id for the given kind)

If either `cursor.kind` or `cursor.id` is present, both MUST be present.

#### Scenario: State snapshot is created as deterministic JSON
- **WHEN** the state store writes a state snapshot
- **THEN** `.aos/state/state.json` exists and is deterministic JSON

#### Scenario: Cursor reference fields are optional
- **GIVEN** a valid state snapshot that includes an empty `cursor` object
- **WHEN** workspace validation is executed
- **THEN** validation succeeds without requiring `cursor.kind` or `cursor.id`

#### Scenario: Cursor kind and id must appear together
- **GIVEN** a state snapshot where `cursor.kind` exists but `cursor.id` is missing (or vice versa)
- **WHEN** workspace validation is executed
- **THEN** validation fails and reports a malformed cursor reference

