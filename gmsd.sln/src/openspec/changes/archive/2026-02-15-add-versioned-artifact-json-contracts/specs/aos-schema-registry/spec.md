## ADDED Requirements
### Requirement: Artifact schema catalog defines canonical versions
The schema registry SHALL define a canonical schema identity and version policy for each workflow artifact type that participates in planning, execution, verification, or command suggestion.

For each registered artifact type, the registry MUST define:
- canonical schema `$id`
- canonical current `schemaVersion`
- optional list of supported previous `schemaVersion` values
- deterministic failure behavior for unsupported versions

#### Scenario: Registry resolves canonical version metadata for task plan artifacts
- **GIVEN** a task plan artifact type is registered in the local schema pack
- **WHEN** a validator resolves schema metadata for `plan.json`
- **THEN** it obtains a canonical schema `$id`, current `schemaVersion`, and supported version policy from the registry

### Requirement: Unsupported artifact schema versions are rejected deterministically
Validators consuming schema registry metadata MUST reject artifacts whose declared `schemaVersion` is not supported for the resolved schema `$id`.

#### Scenario: Validation rejects unsupported schema version
- **GIVEN** an artifact declares `schemaVersion: 99`
- **AND** the registry metadata for that artifact type does not support version 99
- **WHEN** validation is executed
- **THEN** validation fails with an actionable error including artifact path, schema `$id`, declared version, and supported versions
