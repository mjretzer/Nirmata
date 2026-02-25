## MODIFIED Requirements
### Requirement: Phase Planner emits canonical task plan contract
The `PhasePlanner` MUST emit `plan.json` using the canonical task-plan schema and the canonical typed model.

The emitted plan MUST:
- include `schemaVersion` from canonical schema definition
- encode `fileScopes` as an array of objects with canonical `path` field
- use canonical field names for all task plan properties
- conform to the registered task-plan schema before persistence
- validate against the schema registry's canonical task plan schema

#### Scenario: Phase planner writes schema-valid canonical plan
- **GIVEN** a phase decomposition result is ready to persist
- **WHEN** the phase planner writes `.aos/spec/tasks/<task-id>/plan.json`
- **THEN** the file is deterministic JSON that validates against the canonical task-plan schema with canonical `fileScopes[].path` and includes proper `schemaVersion`

### Requirement: Phase planning rejects invalid plan artifacts before persistence
The phase planning workflow MUST validate candidate task-plan JSON against the canonical schema from the schema registry before writing to disk.

Validation MUST use the schema registry to resolve the correct schema version and enforce strict validation rules.

#### Scenario: Invalid file scope shape is rejected during phase planning
- **GIVEN** a candidate task plan where `fileScopes` contains string entries instead of objects
- **WHEN** phase planning validation executes using the schema registry
- **THEN** planning fails with a validation diagnostic referencing the canonical schema requirements and does not persist the invalid plan artifact

### Requirement: Phase Planning Output Schema
The `PhasePlanner` MUST produce a JSON object following the strict canonical schema from the schema registry that includes a list of tasks, their file scopes, and verification steps.

The output MUST:
- Use the canonical schema `$id` for phase plans
- Include `schemaVersion` matching the registry's current version
- Follow canonical field naming and data structures
- Validate successfully against the schema registry

#### Scenario: Valid Phase Plan Generation with Canonical Schema
- **Given** a phase brief and context
- **When** the `PhasePlanner` is invoked
- **Then** it MUST return a JSON object that:
    - Validates against the canonical phase plan schema from the registry
    - Contains `schemaVersion` matching the registry definition
    - Uses canonical field names: `tasks`, `fileScopes`, `verificationSteps`
    - Follows canonical data structures for each field

### Requirement: Schema Validation on Ingest
`PhasePlannerHandler` MUST validate the LLM-generated JSON against the canonical `PhasePlan` schema from the schema registry before processing or persisting the plan.

Validation MUST:
- Use the schema registry to resolve the correct schema version
- Apply strict validation rules
- Produce normalized diagnostics on validation failures
- Reject plans that don't conform to the canonical schema

#### Scenario: Invalid Phase Plan Rejection with Canonical Schema
- **Given** an LLM response that does not match the canonical `PhasePlan` schema from the schema registry
- **When** `PhasePlannerHandler` receives the response
- **Then** it MUST record a validation failure with normalized diagnostics and either retry or return a failure result

## ADDED Requirements
### Requirement: Phase planner validates writer output before persistence
The phase planner MUST validate all generated artifacts against canonical schemas before writing to the filesystem.

Validation MUST:
- Use the schema registry to resolve appropriate schemas
- Apply strict validation for all required fields
- Fail before writing if validation fails
- Produce diagnostic artifacts for validation failures

#### Scenario: Phase planner validation failure before write
- **GIVEN** a generated phase plan that violates the canonical schema
- **WHEN** the phase planner attempts to persist the artifact
- **THEN** validation fails, no file is written, and a diagnostic artifact is created

### Requirement: Phase planner provides normalized validation diagnostics
When schema validation fails, the phase planner MUST generate normalized diagnostic artifacts following the canonical diagnostic schema.

The diagnostic MUST include:
- Schema `$id` and version information
- Specific validation failure details
- Artifact path being validated
- Human-readable repair suggestions
- Canonical diagnostic structure for UI rendering

The diagnostic MUST be written to `.aos/diagnostics/phase-planning/{task-id}.diagnostic.json` before any artifact write is attempted.

#### Scenario: Phase planner creates canonical diagnostic artifact
- **GIVEN** phase plan validation fails due to malformed `fileScopes`
- **WHEN** validation error occurs
- **THEN** a diagnostic artifact is written to `.aos/diagnostics/phase-planning/TSK-0001.diagnostic.json` with canonical structure including schema details, specific errors, and repair guidance

### Requirement: Phase planner diagnostic artifact discovery
Diagnostic artifacts created during phase planning MUST be discoverable by UI components and other tools.

Diagnostics MUST:
- Follow deterministic naming: `.aos/diagnostics/phase-planning/{task-id}.diagnostic.json`
- Include complete context for UI rendering (taskId, phase, timestamp, validation errors, repair suggestions)
- Be created before any artifact write attempt
- Persist alongside the failed artifact for easy discovery

#### Scenario: UI discovers phase planning diagnostic artifacts
- **GIVEN** phase planning validation has failed for task TSK-0001
- **WHEN** UI enumerates `.aos/diagnostics/phase-planning/`
- **THEN** it finds `TSK-0001.diagnostic.json` with complete validation context and repair suggestions
