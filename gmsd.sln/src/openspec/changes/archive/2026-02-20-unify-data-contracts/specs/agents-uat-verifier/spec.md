## MODIFIED Requirements
### Requirement: UAT artifacts written to evidence store
The system SHALL write UAT results to `.aos/evidence/runs/<run-id>/artifacts/uat-results.json` using the canonical verifier output schema from the schema registry.

The artifact MUST:
- Use deterministic JSON serialization
- Include `schemaVersion` matching the canonical schema definition
- Follow canonical field names and data structures
- Validate successfully against the schema registry's verifier output schema

The artifact MUST include:
- `schemaVersion` (integer): Current canonical version from registry
- `runId` (string): The verified run ID
- `taskId` (string): The verified task ID
- `status` (string): "passed" or "failed" (canonical values)
- `timestamp` (string): ISO-8601 timestamp
- `checks` (array): Each check with canonical structure: `type`, `target`, `status`, `message`, `durationMs`

#### Scenario: Successful verification writes canonical uat-results.json
- **GIVEN** a verification that passes all checks
- **WHEN** verification completes
- **THEN** `.aos/evidence/runs/RUN-*/artifacts/uat-results.json` exists with canonical schema, proper `schemaVersion`, and validates against the schema registry

#### Scenario: Failed verification captures canonical failure details
- **GIVEN** a verification with 2 passing and 1 failing check
- **WHEN** verification completes
- **THEN** `uat-results.json` has canonical schema with status "failed", proper `schemaVersion`, and detailed failure information following the canonical structure

### Requirement: UAT spec artifacts track acceptance definitions
The system SHALL write UAT specifications to `.aos/spec/uat/UAT-{taskId}.json` when UAT is defined, using the canonical verifier input schema from the schema registry.

The UAT spec artifact MUST:
- Follow the canonical verifier input schema structure
- Include `schemaVersion` matching the canonical definition
- Use canonical field names for acceptance criteria
- Validate against the schema registry

The UAT spec artifact MUST include:
- `schemaVersion` (integer): Current canonical version from registry
- `taskId` (string): The task ID this UAT belongs to
- `acceptanceCriteria` (array): The defined checks using canonical structure
- `createdAt` (string): ISO-8601 timestamp
- `updatedAt` (string): ISO-8601 timestamp

#### Scenario: UAT definition persisted with canonical schema
- **GIVEN** a task TSK-0003 with acceptance criteria
- **WHEN** the UAT is created/updated
- **THEN** `.aos/spec/uat/UAT-TSK-0003.json` exists with canonical schema, proper `schemaVersion`, and validates against the schema registry

## ADDED Requirements
### Requirement: Verifier validates input contracts on read
The UAT verifier MUST validate UAT specification artifacts against the canonical verifier input schema from the schema registry before processing.

Validation MUST occur before verification execution and MUST use the schema registry to resolve the correct schema version.

#### Scenario: Verifier rejects invalid UAT specification before verification
- **GIVEN** `.aos/spec/uat/UAT-TSK-0001.json` exists but violates the canonical verifier input schema
- **WHEN** `VerifyAsync` is invoked for task TSK-0001
- **THEN** verification fails before execution and returns a deterministic validation failure with schema details

### Requirement: Verifier validates output contracts before writing
The UAT verifier MUST validate verification result artifacts against the canonical verifier output schema from the schema registry before writing to evidence.

Validation MUST:
- Use the schema registry to resolve the correct schema version
- Apply strict validation for all required fields
- Fail before writing if validation fails
- Produce normalized diagnostic artifacts for validation failures

#### Scenario: Verifier validation failure prevents evidence write
- **GIVEN** verification produces results that violate the canonical verifier output schema
- **WHEN** the verifier attempts to write results
- **THEN** validation fails, no evidence is written, and a diagnostic artifact is created

### Requirement: Verifier provides normalized validation diagnostics
When schema validation fails during verification, the verifier MUST generate normalized diagnostic artifacts following the canonical diagnostic schema.

The diagnostic MUST include:
- Schema `$id` and version from the registry
- Specific validation failure details
- Artifact path being validated
- Verification context (task ID, run ID)
- Human-readable repair suggestions
- Canonical diagnostic structure for UI rendering

The diagnostic MUST be written to `.aos/diagnostics/verification/{run-id}-{task-id}.diagnostic.json` before any artifact write is attempted.

#### Scenario: Verifier creates canonical diagnostic for validation failure
- **GIVEN** UAT specification validation fails due to malformed acceptance criteria
- **WHEN** validation error occurs
- **THEN** a diagnostic artifact is written to `.aos/diagnostics/verification/RUN-0001-TSK-0001.diagnostic.json` with canonical structure including schema details, specific errors, verification context, and repair guidance

### Requirement: Verifier diagnostic artifact discovery
Diagnostic artifacts created during verification MUST be discoverable by UI components and other tools.

Diagnostics MUST:
- Follow deterministic naming: `.aos/diagnostics/verification/{run-id}-{task-id}.diagnostic.json`
- Include complete context for UI rendering (taskId, runId, phase, timestamp, validation errors, repair suggestions)
- Be created before any artifact write attempt
- Persist alongside the failed artifact for easy discovery

#### Scenario: UI discovers verification diagnostic artifacts
- **GIVEN** verification validation has failed for run RUN-0001 and task TSK-0001
- **WHEN** UI enumerates `.aos/diagnostics/verification/`
- **THEN** it finds `RUN-0001-TSK-0001.diagnostic.json` with complete validation context and repair suggestions

### Requirement: Verifier extracts acceptance criteria using unified contracts
The verifier MUST extract and process acceptance criteria from UAT specifications using the unified contract format.

Extraction MUST:
- Use the canonical verifier input schema structure
- Validate criteria format against the schema
- Support all canonical check types from the schema
- Produce normalized validation errors for malformed criteria

#### Scenario: Verifier processes unified acceptance criteria
- **GIVEN** a UAT specification with canonical acceptance criteria structure
- **WHEN** the verifier extracts criteria for execution
- **THEN** it validates against the canonical schema and processes all criteria using the unified format
