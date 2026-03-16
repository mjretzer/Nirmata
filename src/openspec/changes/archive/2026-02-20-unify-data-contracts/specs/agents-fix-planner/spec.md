## MODIFIED Requirements
### Requirement: Fix planner emits canonical schema-valid plan artifacts
The fix planner MUST emit `plan.json` using the canonical task-plan/fix-plan schema definitions and typed contract models shared with other workflow consumers.

Each emitted fix `plan.json` MUST:
- include `schemaVersion` from the canonical schema definition
- represent `fileScopes` with canonical object entries using `path`
- represent acceptance/verification structures using canonical contract fields
- validate against the registered schema before persistence
- Use the schema registry to resolve the correct schema version

#### Scenario: Fix planner writes canonical `fileScopes` entries
- **GIVEN** fix planning produces a set of affected files
- **WHEN** it writes `.aos/spec/tasks/<task-id>/plan.json`
- **THEN** each file scope entry is written as an object containing canonical `path`, the artifact includes proper `schemaVersion`, and validates against the canonical schema from the schema registry

### Requirement: Fix task artifacts written to spec store
The system SHALL write fix task artifacts to `.aos/spec/tasks/<task-id>/` following the canonical spec store schema from the schema registry.

Each fix task MUST have three files using canonical schemas:
- `task.json`: Task metadata using canonical task metadata schema
- `plan.json`: Execution plan using canonical task-plan schema  
- `links.json`: Links using canonical links schema

All artifacts MUST:
- Include `schemaVersion` matching canonical definitions
- Validate against the schema registry
- Use canonical field names and data structures

#### Scenario: Successful planning creates complete canonical task artifacts
- **GIVEN** a successful fix planning for issue ISS-0001
- **WHEN** planning completes
- **THEN** `.aos/spec/tasks/TSK-0002/task.json`, `plan.json`, and `links.json` exist with canonical schemas, proper `schemaVersion`, and validate against the schema registry

#### Scenario: Task metadata references parent task and issues with canonical structure
- **GIVEN** fix task TSK-0002 generated from TSK-0001 failure with issue ISS-0001
- **WHEN** examining `task.json`
- **THEN** it contains canonical field names `parentTaskId: "TSK-0001"` and `issueIds: ["ISS-0001"]` with proper `schemaVersion`

## ADDED Requirements
### Requirement: Fix planner validates input contracts on read
The fix planner MUST validate issue artifacts and task plans against canonical schemas from the schema registry before processing.

Validation MUST:
- Use the schema registry to resolve correct schema versions
- Validate issue artifacts against canonical issue schema
- Validate parent task plans against canonical task-plan schema
- Fail before processing if validation fails
- Produce normalized diagnostic artifacts for validation failures

#### Scenario: Fix planner rejects invalid issue artifacts before planning
- **GIVEN** issue artifacts that violate the canonical issue schema from the schema registry
- **WHEN** `PlanFixAsync` is invoked
- **THEN** planning fails before analysis and returns a deterministic validation failure with schema details

### Requirement: Fix planner validates output contracts before writing
The fix planner MUST validate all generated fix task artifacts against canonical schemas from the schema registry before writing to the spec store.

Validation MUST:
- Use the schema registry to resolve correct schema versions
- Apply strict validation for all required fields
- Fail before writing if validation fails
- Produce normalized diagnostic artifacts for validation failures

#### Scenario: Fix planner validation failure prevents artifact write
- **GIVEN** fix planning generates artifacts that violate canonical schemas
- **WHEN** the planner attempts to write task artifacts
- **THEN** validation fails, no artifacts are written, and a diagnostic artifact is created

### Requirement: Fix planner provides normalized validation diagnostics
When schema validation fails during fix planning, the planner MUST generate normalized diagnostic artifacts following the canonical diagnostic schema.

The diagnostic MUST include:
- Schema `$id` and version from the registry
- Specific validation failure details
- Artifact path being validated
- Planning context (issue IDs, parent task ID)
- Human-readable repair suggestions
- Canonical diagnostic structure for UI rendering

The diagnostic MUST be written to `.aos/diagnostics/fix-planning/{task-id}.diagnostic.json` before any artifact write is attempted.

#### Scenario: Fix planner creates canonical diagnostic for validation failure
- **GIVEN** fix plan validation fails due to malformed `fileScopes` structure
- **WHEN** validation error occurs
- **THEN** a diagnostic artifact is written to `.aos/diagnostics/fix-planning/TSK-0002.diagnostic.json` with canonical structure including schema details, specific errors, planning context, and repair guidance

### Requirement: Fix planner diagnostic artifact discovery
Diagnostic artifacts created during fix planning MUST be discoverable by UI components and other tools.

Diagnostics MUST:
- Follow deterministic naming: `.aos/diagnostics/fix-planning/{task-id}.diagnostic.json`
- Include complete context for UI rendering (taskId, parentTaskId, issueIds, phase, timestamp, validation errors, repair suggestions)
- Be created before any artifact write attempt
- Persist alongside the failed artifact for easy discovery

#### Scenario: UI discovers fix planning diagnostic artifacts
- **GIVEN** fix planning validation has failed for task TSK-0002
- **WHEN** UI enumerates `.aos/diagnostics/fix-planning/`
- **THEN** it finds `TSK-0002.diagnostic.json` with complete validation context and repair suggestions

### Requirement: Fix planner consumes unified task plan contracts
The fix planner MUST read parent task plans using the unified contract format and validate against canonical schemas on read.

Reading MUST:
- Use the schema registry to resolve the correct schema version
- Validate the task plan before analysis
- Produce normalized diagnostics for validation failures
- Extract scope and context using canonical field names

#### Scenario: Fix planner reads and validates unified parent task plan
- **GIVEN** a parent task plan artifact using the unified contract format
- **WHEN** the fix planner reads the plan for context
- **THEN** it validates against the canonical schema and proceeds only if validation passes
