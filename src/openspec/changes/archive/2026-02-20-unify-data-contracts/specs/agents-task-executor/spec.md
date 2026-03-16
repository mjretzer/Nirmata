## MODIFIED Requirements
### Requirement: Task executor validates plan contract on read
The task executor MUST validate task plan artifacts against the canonical registered schema from the schema registry before execution.

Validation MUST occur before scope enforcement, step execution, or evidence mutation and MUST use the schema registry to resolve the correct schema version.

#### Scenario: Executor rejects invalid task plan contract before execution
- **GIVEN** `.aos/spec/tasks/TSK-0001/plan.json` exists but violates the canonical schema from the schema registry
- **WHEN** `ExecuteAsync` is invoked for `TSK-0001`
- **THEN** execution fails before any file mutation and returns a deterministic validation failure with schema details

### Requirement: Task executor emits friendly diagnostics for contract failures
When task plan contract validation fails, the executor MUST emit a diagnostic artifact containing actionable validation details following the canonical diagnostic schema.

The diagnostic MUST include:
- contract path
- canonical schema `$id` from registry
- schema version details (declared and supported)
- human-readable validation messages
- canonical diagnostic structure for UI rendering

#### Scenario: Executor writes validation diagnostic artifact for invalid plan
- **GIVEN** execution encounters a schema-invalid `plan.json`
- **WHEN** task execution aborts due to contract validation
- **THEN** a diagnostic artifact is written under the run evidence path with canonical validation details suitable for UI rendering

## ADDED Requirements
### Requirement: Task executor validates evidence artifacts before writing
The task executor MUST validate all evidence artifacts against canonical schemas before writing to the evidence store.

Validation MUST:
- Use the schema registry to resolve appropriate schemas for each artifact type
- Apply strict validation for execution results, file changes, and metadata
- Fail before writing if validation fails
- Produce normalized diagnostic artifacts for validation failures

#### Scenario: Task executor evidence validation failure
- **GIVEN** task execution produces evidence that violates the canonical evidence schema
- **WHEN** the executor attempts to write evidence artifacts
- **THEN** validation fails, no evidence is written, and a diagnostic artifact is created

### Requirement: Task executor provides normalized contract validation diagnostics
When any schema validation fails during task execution, the executor MUST generate normalized diagnostic artifacts following the canonical diagnostic schema.

The diagnostic MUST include:
- Schema `$id` and version from the registry
- Specific validation failure details
- Artifact path being validated
- Execution context (task ID, run ID)
- Human-readable repair suggestions
- Canonical diagnostic structure for UI rendering

The diagnostic MUST be written to `.aos/diagnostics/task-execution/{run-id}-{task-id}.diagnostic.json` before any artifact write is attempted.

#### Scenario: Task executor creates canonical diagnostic for evidence validation
- **GIVEN** task execution evidence validation fails due to malformed result structure
- **WHEN** validation error occurs
- **THEN** a diagnostic artifact is written to `.aos/diagnostics/task-execution/RUN-0001-TSK-0001.diagnostic.json` with canonical structure including schema details, specific errors, execution context, and repair guidance

### Requirement: Task executor diagnostic artifact discovery
Diagnostic artifacts created during task execution MUST be discoverable by UI components and other tools.

Diagnostics MUST:
- Follow deterministic naming: `.aos/diagnostics/task-execution/{run-id}-{task-id}.diagnostic.json`
- Include complete context for UI rendering (taskId, runId, phase, timestamp, validation errors, repair suggestions)
- Be created before any artifact write attempt
- Persist alongside the failed artifact for easy discovery

#### Scenario: UI discovers task execution diagnostic artifacts
- **GIVEN** task execution validation has failed for run RUN-0001 and task TSK-0001
- **WHEN** UI enumerates `.aos/diagnostics/task-execution/`
- **THEN** it finds `RUN-0001-TSK-0001.diagnostic.json` with complete validation context and repair suggestions

### Requirement: Task executor reads unified contract artifacts
The task executor MUST read task plan artifacts using the unified contract format and validate against canonical schemas on read.

Reading MUST:
- Use the schema registry to resolve the correct schema version
- Validate the artifact before processing
- Produce normalized diagnostics for validation failures
- Support graceful degradation for minor schema version differences

#### Scenario: Task executor reads and validates unified task plan
- **GIVEN** a task plan artifact using the unified contract format
- **WHEN** the executor reads the plan for execution
- **THEN** it validates against the canonical schema and proceeds only if validation passes
