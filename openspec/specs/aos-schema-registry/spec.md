# aos-schema-registry Specification

## Purpose
TBD - created by archiving change add-schema-registry-and-validation-gate. Update Purpose after archive.
## Requirements
### Requirement: Engine ships an embedded JSON Schema pack for AOS artifacts
The system SHALL ship the JSON Schemas required to validate AOS workspace artifacts as **embedded template assets** owned by `Gmsd.Aos`.

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

The stable identifier MUST be the JSON Schema `$id` value.

For the local schema pack, schema selection SHOULD be mediated via the local registry (`.aos/schemas/registry.json`) to avoid ad-hoc path guessing.

#### Scenario: Validator resolves the project schema deterministically from the local registry
- **GIVEN** a repository where `aos init` has created a local schema pack under `.aos/schemas/**`
- **WHEN** the validator needs to validate `.aos/spec/project.json`
- **THEN** it resolves the corresponding schema by `$id` deterministically (without ad-hoc filename guessing)

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
The system SHALL expose a stable catalog of schema IDs in `Gmsd.Aos.Public.Catalogs.SchemaIds` for use by validators and tooling.

The catalog MUST use the canonical JSON Schema `$id` values (e.g., `gmsd:aos:schema:project:v1`).

#### Scenario: Validator references schema IDs without ad-hoc string literals
- **WHEN** engine validation selects a schema for `.aos/spec/project.json`
- **THEN** it references the schema ID from `Gmsd.Aos.Public.Catalogs.SchemaIds`

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

