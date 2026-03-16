## MODIFIED Requirements

### Requirement: Engine ships an embedded JSON Schema pack for AOS artifacts
The system SHALL ship the JSON Schemas required to validate AOS workspace artifacts as **embedded template assets** owned by `nirmata.Aos`.

The embedded schema pack MUST be versioned with the engine code so bootstrapping behavior is deterministic across machines.

`aos init` SHALL materialize the embedded template pack into a **local schema pack** under `.aos/schemas/**` in the target repository.

`aos validate schemas` SHALL validate the **local schema pack** under `.aos/schemas/**` (not the embedded template pack).

#### Scenario: Local schema pack is available without network access
- **GIVEN** `aos init` has created `.aos/schemas/**` in a repository
- **WHEN** `aos validate schemas` is executed in that repository
- **THEN** schema loading succeeds without requiring network access or external services

### Requirement: Canonical schema filenames are enforced deterministically
The system SHALL enforce canonical schema filenames for **local schema pack** assets under `.aos/schemas/**`.

Canonical schema filenames MUST:
- use lower-kebab-case for the base name (e.g., `context-pack`)
- end with `.schema.json`
- contain no additional `.` characters besides the `.schema.json` suffix

#### Scenario: Non-canonical local schema filenames are rejected
- **GIVEN** a local schema pack asset named `context.pack.schema.json`
- **WHEN** `aos validate schemas` is executed
- **THEN** schema loading fails with an actionable error identifying the non-canonical filename and the expected canonical name

### Requirement: Schemas can be addressed by a stable identifier
The system SHALL provide a stable identifier for each schema so validators can select the correct schema for a given artifact type.

For the local schema pack, the stable identifier SHOULD be derived from the local registry (`.aos/schemas/registry.json`) rather than ad-hoc path guessing.

#### Scenario: Validator resolves the project schema deterministically from the local registry
- **GIVEN** a repository where `aos init` has created a local schema pack under `.aos/schemas/**`
- **WHEN** the validator needs to validate `.aos/spec/project.json`
- **THEN** it resolves the corresponding schema by a stable identifier from the local registry

## ADDED Requirements

### Requirement: Local schema pack registry defines the pack contents
The system SHALL treat `.aos/schemas/registry.json` as the authoritative inventory of schema files in the local schema pack.

`registry.json` MUST be valid JSON and MUST conform to the schema defined by `.aos/schemas/schema-registry.schema.json`.

`registry.json.schemas` MUST:
- be a non-empty array
- contain **canonical** schema filenames relative to `.aos/schemas/` (filenames only; no directory separators)
- contain no duplicates

For each entry in `registry.json.schemas`, the referenced file MUST exist under `.aos/schemas/` and MUST be valid JSON.

#### Scenario: Missing local registry fails with an actionable error
- **GIVEN** a repository with no `.aos/schemas/registry.json`
- **WHEN** `aos validate schemas` is executed
- **THEN** the command fails with an actionable error (e.g., instructing the user to run `aos init`)

#### Scenario: Empty local schema inventory is rejected
- **GIVEN** `.aos/schemas/registry.json` exists and conforms to the registry schema but contains an empty `schemas` array
- **WHEN** `aos validate schemas` is executed
- **THEN** the command fails with an actionable error indicating that at least one schema must be registered

#### Scenario: Registry entries must exist on disk
- **GIVEN** `.aos/schemas/registry.json` lists `project.schema.json` but the file does not exist under `.aos/schemas/`
- **WHEN** `aos validate schemas` is executed
- **THEN** the command fails with an actionable error identifying the missing referenced schema file

