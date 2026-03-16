## ADDED Requirements
### Requirement: Engine ships an embedded JSON Schema pack for AOS artifacts
The system SHALL ship the JSON Schemas required to validate AOS workspace artifacts as embedded assets owned by `nirmata.Aos`.

The embedded schema pack MUST be versioned with the engine code so validation behavior is deterministic across machines.

#### Scenario: Embedded schemas are available without network access
- **WHEN** `aos validate schemas` is executed in a repository
- **THEN** schema loading succeeds without requiring network access or external services

### Requirement: Canonical schema filenames are enforced deterministically
The system SHALL enforce canonical schema filenames for shipped schema assets.

Canonical schema filenames MUST:
- use lower-kebab-case for the base name (e.g., `context-pack`)
- end with `.schema.json`
- contain no additional `.` characters besides the `.schema.json` suffix

#### Scenario: Non-canonical schema filenames are rejected
- **GIVEN** a shipped schema asset named `context.pack.schema.json`
- **WHEN** schemas are loaded for validation
- **THEN** schema loading fails with an actionable error identifying the non-canonical filename and the expected canonical name

### Requirement: Schemas can be addressed by a stable identifier
The system SHALL provide a stable identifier for each shipped schema so validators can select the correct schema for a given artifact type.

#### Scenario: Validator resolves the project schema deterministically
- **WHEN** the validator needs to validate `.aos/spec/project.json`
- **THEN** it resolves the corresponding shipped schema by a stable identifier (not by ad-hoc path guessing)

