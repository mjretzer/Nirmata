## ADDED Requirements

### Requirement: Embedded schemas are loaded by canonical JSON Schema `$id`
The system SHALL treat JSON Schema `$id` as the canonical schema identifier for AOS artifacts.

The engine-owned embedded schema registry MUST load embedded schemas deterministically and index them by `$id`.

The embedded schema registry MUST fail fast if:
- any embedded schema is missing `$id`
- any embedded schema `$id` is duplicated across the embedded pack

#### Scenario: Embedded schema registry rejects a missing `$id`
- **GIVEN** an embedded schema file that omits `$id`
- **WHEN** the embedded schema registry is loaded
- **THEN** schema loading fails deterministically with an actionable error identifying the schema file

#### Scenario: Embedded schema registry rejects duplicate `$id`
- **GIVEN** two embedded schema files with the same `$id`
- **WHEN** the embedded schema registry is loaded
- **THEN** schema loading fails deterministically with an actionable error identifying the conflicting schema files and `$id`

### Requirement: Schema IDs are enumerated in a stable public catalog
The system SHALL expose a stable catalog of schema IDs in `nirmata.Aos.Public.Catalogs.SchemaIds` for use by validators and tooling.

The catalog MUST use the canonical JSON Schema `$id` values (e.g., `nirmata:aos:schema:project:v1`).

#### Scenario: Validator references schema IDs without ad-hoc string literals
- **WHEN** engine validation selects a schema for `.aos/spec/project.json`
- **THEN** it references the schema ID from `nirmata.Aos.Public.Catalogs.SchemaIds`

### Requirement: Local schema pack can be validated for ID and draft compliance
`aos validate schemas` SHALL validate the local schema pack under `.aos/schemas/**` and fail if any schema:
- is malformed JSON
- is missing `$schema`, `$id`, or `type`
- uses an unsupported `$schema` draft URI
- duplicates a `$id` value within the local pack

#### Scenario: Local schema pack rejects duplicate `$id`
- **GIVEN** a local schema pack where two schema files share the same `$id`
- **WHEN** `aos validate schemas` is executed
- **THEN** schema validation fails and reports the duplicate `$id` and the schema files involved

## MODIFIED Requirements

### Requirement: Schemas can be addressed by a stable identifier
The system SHALL provide a stable identifier for each schema so validators can select the correct schema for a given artifact type.

The stable identifier MUST be the JSON Schema `$id` value.

For the local schema pack, schema selection SHOULD be mediated via the local registry (`.aos/schemas/registry.json`) to avoid ad-hoc path guessing.

#### Scenario: Validator resolves the project schema deterministically from the local registry
- **GIVEN** a repository where `aos init` has created a local schema pack under `.aos/schemas/**`
- **WHEN** the validator needs to validate `.aos/spec/project.json`
- **THEN** it resolves the corresponding schema by `$id` deterministically (without ad-hoc filename guessing)
