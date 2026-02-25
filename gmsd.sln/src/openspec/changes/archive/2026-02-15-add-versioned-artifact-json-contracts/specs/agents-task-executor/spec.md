## ADDED Requirements
### Requirement: Task executor validates plan contract on read
The task executor MUST validate task plan artifacts against the canonical registered schema before execution.

Validation MUST occur before scope enforcement, step execution, or evidence mutation.

#### Scenario: Executor rejects invalid task plan contract before execution
- **GIVEN** `.aos/spec/tasks/TSK-0001/plan.json` exists but violates the canonical schema
- **WHEN** `ExecuteAsync` is invoked for `TSK-0001`
- **THEN** execution fails before any file mutation and returns a deterministic validation failure

### Requirement: Task executor emits friendly diagnostics for contract failures
When task plan contract validation fails, the executor MUST emit a diagnostic artifact containing actionable validation details.

The diagnostic MUST include:
- contract path
- schema `$id`
- schema version details (declared and supported)
- human-readable validation messages

#### Scenario: Executor writes validation diagnostic artifact for invalid plan
- **GIVEN** execution encounters a schema-invalid `plan.json`
- **WHEN** task execution aborts due to contract validation
- **THEN** a diagnostic artifact is written under the run evidence path with validation details suitable for UI rendering
