# agents-task-executor Specification

## ADDED Requirements

### Requirement: Task Executor interface exists
The system SHALL provide an `ITaskExecutor` interface in `Gmsd.Agents.Execution.Execution.TaskExecutor` that executes task plans sequentially with strict file scoping and evidence capture.

The interface MUST define:
- `ExecuteAsync(TaskExecutionRequest request, CancellationToken ct)` → returns `Task<TaskExecutionResult>`

`TaskExecutionRequest` MUST include:
- `TaskId` (string): Canonical task ID in format `TSK-######`
- `PlanPath` (string): Path to the task plan file (`.aos/spec/tasks/<task-id>/plan.json`)
- `ContextPackId` (string|null): Optional context pack to load for execution
- `RunId` (string|null): Parent run ID for correlation

`TaskExecutionResult` MUST include:
- `IsSuccess` (bool): Whether execution completed successfully
- `TaskId` (string): The executed task ID
- `RunId` (string): The run record ID created for this execution
- `FilesModified` (array): List of file paths modified
- `DiffStat` (object): Files changed, insertions, deletions counts
- `ErrorMessage` (string|null): Error details if failed

#### Scenario: Task executor executes a valid task plan
- **GIVEN** a valid task plan at `.aos/spec/tasks/TSK-0001/plan.json`
- **WHEN** `ExecuteAsync` is called with the task ID
- **THEN** the plan is executed, files are modified within scope, and a success result is returned with the run ID

#### Scenario: Task executor rejects out-of-scope modifications
- **GIVEN** a task plan with file scopes limited to `src/services/`
- **WHEN** the execution attempts to modify `src/api/Program.cs`
- **THEN** the modification is rejected, execution fails, and the error indicates scope violation

### Requirement: Task Executor enforces strict file scope
The system SHALL enforce that task execution only modifies files explicitly listed in the task plan's `fileScopes` array.

The executor MUST:
- Parse `fileScopes` from `plan.json` before execution
- Reject any file modification attempt outside the allowed scope
- Report scope violations as deterministic errors with actionable messages
- Not apply partial changes when scope violations are detected

#### Scenario: Executor allows in-scope file modifications
- **GIVEN** a task plan with `fileScopes: ["src/models/User.cs", "src/services/AuthService.cs"]`
- **WHEN** execution modifies only those two files
- **THEN** the execution succeeds and files are updated

#### Scenario: Executor blocks out-of-scope file modifications
- **GIVEN** a task plan with `fileScopes: ["src/models/"]`
- **WHEN** execution attempts to modify `tests/UserTests.cs`
- **THEN** the execution fails before any files are modified, with error "File 'tests/UserTests.cs' is outside allowed scope 'src/models/'"

### Requirement: Task Executor creates distinct RUN records per task
The system SHALL create a new run record for each task execution using the run lifecycle infrastructure.

Each task execution MUST:
- Call `IRunLifecycleManager.StartRunAsync` to create a new run
- Associate the run with the task via `taskId` metadata
- Write execution evidence to `.aos/evidence/runs/<run-id>/`
- Finish the run via `IRunLifecycleManager.FinishRunAsync` on completion or failure

#### Scenario: Task execution creates run record
- **GIVEN** a task ready for execution
- **WHEN** `ExecuteAsync` is invoked
- **THEN** a new `.aos/evidence/runs/RUN-*/` folder is created with run metadata

#### Scenario: Task run record contains task correlation
- **GIVEN** a completed task execution for TSK-0001
- **WHEN** examining the run metadata at `.aos/evidence/runs/<run-id>/run.json`
- **THEN** it contains `taskId: "TSK-0001"` and references to the task plan

### Requirement: Task Executor updates task evidence pointers
The system SHALL update the task evidence `latest.json` pointer upon successful task completion.

The executor MUST:
- On success, write or update `.aos/evidence/task-evidence/<task-id>/latest.json`
- Include `taskId`, `runId`, `gitCommit`, and `diffstat` in the pointer
- Use deterministic JSON serialization per `aos-deterministic-json-serialization`

#### Scenario: Task completion updates latest pointer
- **GIVEN** a successful task execution for TSK-0001
- **WHEN** the execution completes
- **THEN** `.aos/evidence/task-evidence/TSK-0001/latest.json` exists with correct run ID and diff stats

#### Scenario: Repeated executions update latest pointer
- **GIVEN** TSK-0001 has been executed twice (RUN-001, RUN-002)
- **WHEN** examining `.aos/evidence/task-evidence/TSK-0001/latest.json`
- **THEN** it points to RUN-002 (the most recent successful execution)

### Requirement: Task Executor updates cursor and task status deterministically
The system SHALL update workspace state to reflect task execution progress and completion.

The executor MUST:
- Append execution start event to `.aos/state/events.ndjson`
- Update task status in `.aos/state/state.json` (pending → running → completed|failed)
- Update cursor position if this task completes a phase milestone
- Use deterministic JSON serialization for all state writes

#### Scenario: Task status transitions during execution
- **GIVEN** a task in "pending" status
- **WHEN** execution starts and then completes
- **THEN** state transitions are recorded: pending → running (on start), running → completed (on success)

#### Scenario: Failed task updates status to failed
- **GIVEN** a task in "running" status
- **WHEN** execution fails due to scope violation
- **THEN** the task status is updated to "failed" with error details

### Requirement: Task Executor provides normalized results
The system SHALL produce normalized execution results suitable for orchestrator verification.

`TaskExecutionResult` MUST include normalized fields:
- `NormalizedOutput` (object): Structured output with `action`, `files`, `summary`
- `VerificationArtifacts` (array): Pointers to evidence that can be verified
- `DeterministicHash` (string): Hash of the result for idempotency checks

#### Scenario: Execution produces normalized output
- **GIVEN** a successful task execution
- **WHEN** examining the result
- **THEN** `NormalizedOutput` contains structured data: action type, affected files, and summary

### Requirement: Task Executor Handler integrates with orchestrator
The system SHALL provide a `TaskExecutorHandler` that integrates with the orchestrator's gating and dispatch system.

The handler MUST:
- Implement the handler pattern used by the orchestrator
- Accept an execution intent with task reference
- Delegate to `ITaskExecutor` for actual execution
- Return a handler result indicating success/failure and next phase
- Route to Verifier phase on success, FixPlanner on failure

#### Scenario: Handler executes task and routes to Verifier
- **GIVEN** an execution intent for TSK-0001
- **WHEN** the handler is invoked by the orchestrator
- **THEN** it executes the task and returns success with next phase "Verifier"

#### Scenario: Handler routes to FixPlanner on failure
- **GIVEN** an execution intent for a task with scope violations
- **WHEN** the handler is invoked and execution fails
- **THEN** it returns failure result with next phase "FixPlanner" and error details
