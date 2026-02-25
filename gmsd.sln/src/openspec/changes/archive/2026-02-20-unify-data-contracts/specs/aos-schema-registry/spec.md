## ADDED Requirements
### Requirement: Canonical artifact schema definitions
The schema registry SHALL define canonical JSON schemas for all workflow artifacts that participate in phase planning, task execution, verification, and fix planning.

The registry MUST include schemas for:
- Phase plan artifacts (`.aos/spec/phases/{phase-id}/plan.json`)
- Task plan artifacts (`.aos/spec/tasks/{task-id}/plan.json`) 
- Verifier input artifacts (`.aos/spec/uat/UAT-{task-id}.json`)
- Verifier output artifacts (`.aos/evidence/runs/{run-id}/artifacts/uat-results.json`)
- Fix plan artifacts (`.aos/spec/tasks/{fix-task-id}/plan.json`)
- Diagnostic artifacts (`.aos/diagnostics/{phase}/{artifact-id}.diagnostic.json`)

Each schema MUST:
- Include `$id` with canonical identifier format `gmsd:aos:schema:{artifact-type}:v{version}`
- Define `schemaVersion` field requirements
- Use strict validation for required fields and data types
- Include clear validation messages for common errors
- Provide JSON Schema examples for valid artifacts

#### Scenario: Registry defines unified phase plan schema
- **GIVEN** the aos-schema-registry is initialized
- **WHEN** querying for phase plan schema
- **THEN** it returns a schema with `$id: "gmsd:aos:schema:phase-plan:v1"` and strict validation rules

#### Scenario: Registry defines unified task plan schema
- **GIVEN** the aos-schema-registry is initialized  
- **WHEN** querying for task plan schema
- **THEN** it returns a schema with `$id: "gmsd:aos:schema:task-plan:v1"` and canonical `fileScopes` object format

#### Scenario: Registry defines diagnostic artifact schema
- **GIVEN** the aos-schema-registry is initialized
- **WHEN** querying for diagnostic artifact schema
- **THEN** it returns a schema with `$id: "gmsd:aos:schema:diagnostic:v1"` with fields for validation errors, repair suggestions, and context

### Requirement: Diagnostic artifact schema definition
The schema registry SHALL define a canonical diagnostic artifact schema for all validation failures across workflow phases.

The diagnostic schema MUST include:
- `schemaVersion` (integer): Current canonical version from registry
- `schemaId` (string): Always `"gmsd:aos:schema:diagnostic:v1"`
- `artifactPath` (string): Path to the artifact that failed validation
- `failedSchemaId` (string): The schema that validation failed against
- `failedSchemaVersion` (integer): The schema version that failed
- `timestamp` (string): ISO-8601 timestamp when diagnostic was created
- `phase` (string): Workflow phase where validation failed (phase-planning, task-execution, verification, fix-planning)
- `context` (object): Operation context with taskId, runId, or other relevant identifiers
- `validationErrors` (array): Array of validation failure objects with path, message, expected, actual
- `repairSuggestions` (array): Array of actionable repair guidance strings

#### Scenario: Diagnostic artifact created for validation failure
- **GIVEN** a task plan validation fails due to malformed fileScopes
- **WHEN** validation error occurs
- **THEN** a diagnostic artifact is created with schema `gmsd:aos:schema:diagnostic:v1` containing specific errors and repair suggestions

### Requirement: JSON Schema examples for all canonical schemas
The schema registry SHALL provide JSON Schema examples and sample valid artifacts for each canonical schema.

For each schema, the registry MUST provide:
- Complete JSON Schema definition with all required fields
- Example of a valid artifact matching the schema
- Common validation failure patterns with error messages
- Migration rules for transforming old formats to new schema

#### Scenario: Schema registry provides task plan schema with examples
- **GIVEN** a developer needs to understand task plan schema
- **WHEN** querying the registry for task plan schema
- **THEN** it returns the JSON Schema definition, a valid example artifact, and common validation failures

### Requirement: Schema version compatibility management
The schema registry SHALL define version compatibility policies for each artifact type to support migration and backwards compatibility.

For each schema, the registry MUST define:
- Current canonical version
- List of supported previous versions
- Migration rules between versions
- Deprecation timeline for unsupported versions

#### Scenario: Registry supports task plan schema versions
- **GIVEN** a task plan artifact with `schemaVersion: 1`
- **WHEN** validation is performed
- **THEN** the registry confirms version 1 is supported and validates against the correct schema

#### Scenario: Registry rejects unsupported schema version
- **GIVEN** a task plan artifact with `schemaVersion: 99`
- **WHEN** validation is performed
- **THEN** validation fails with clear error about unsupported version and available alternatives

### Requirement: Unified contract validation catalog
The schema registry SHALL provide a unified catalog of all workflow artifact contracts with their canonical schemas and validation rules.

The catalog MUST:
- Expose schema IDs via `Gmsd.Aos.Public.Catalogs.SchemaIds`
- Provide schema metadata (version, compatibility, migration info)
- Support schema resolution by artifact type and version
- Include validation helpers for common workflow operations

#### Scenario: Catalog resolves task plan schema deterministically
- **GIVEN** a task plan artifact needs validation
- **WHEN** calling catalog resolver for artifact type "task-plan" and version 1
- **THEN** it returns the correct schema instance without path guessing

## MODIFIED Requirements
### Requirement: Artifact schema catalog defines canonical versions
The schema registry SHALL define a canonical schema identity and version policy for each workflow artifact type that participates in planning, execution, verification, or command suggestion.

For each registered artifact type, the registry MUST define:
- canonical schema `$id`
- canonical current `schemaVersion`
- optional list of supported previous `schemaVersion` values
- deterministic failure behavior for unsupported versions
- unified validation rules across all workflow phases

#### Scenario: Registry resolves canonical version metadata for unified task plan artifacts
- **GIVEN** a task plan artifact type is registered in the local schema pack
- **WHEN** a validator resolves schema metadata for `plan.json`
- **THEN** it obtains a canonical schema `$id`, current `schemaVersion`, supported version policy, and unified validation rules from the registry

#### Scenario: Registry provides unified contract validation across phases
- **GIVEN** multiple workflow phases need to validate the same artifact type
- **WHEN** each phase requests schema validation
- **THEN** all phases receive identical schema definitions and validation results
