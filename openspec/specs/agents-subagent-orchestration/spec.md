# agents-subagent-orchestration Specification

## Purpose
TBD - created by archiving change add-task-executor-workflow. Update Purpose after archive.
## Requirements
### Requirement: Subagent Orchestrator interface exists
The system SHALL provide an `ISubagentOrchestrator` interface in `Gmsd.Agents.Execution.Execution.SubagentRuns` that manages per-task subagent lifecycle with fresh context and bounded context packs.

The interface MUST define:
- `RunSubagentAsync(SubagentRunRequest request, CancellationToken ct)` → returns `Task<SubagentRunResult>`

`SubagentRunRequest` MUST include:
- `TaskId` (string): Canonical task ID being executed
- `PlanPath` (string): Path to task plan file
- `ContextPackId` (string): Context pack to load for this subagent
- `ParentRunId` (string): Parent run ID for correlation
- `FileScopes` (array): Allowed file modification scopes

`SubagentRunResult` MUST include:
- `IsSuccess` (bool): Whether subagent completed successfully
- `SubagentRunId` (string): The run ID created for this subagent execution
- `Outputs` (array): Files written/modified by the subagent
- `Logs` (array): Log entries captured from subagent
- `Error` (string|null): Error message if failed

#### Scenario: Subagent orchestrator runs fresh subagent for task
- **GIVEN** a task TSK-0001 with context pack PCK-0001
- **WHEN** `RunSubagentAsync` is called
- **THEN** a fresh subagent is spawned with isolated context, executes the task plan, and returns results

#### Scenario: Subagent runs with bounded context pack
- **GIVEN** a context pack with specific file budget
- **WHEN** the subagent is spawned
- **THEN** only the bounded context from the pack is available to the subagent (no full workspace access)

### Requirement: One RUN record per atomic task/step
The system SHALL create exactly one RUN record per atomic task or step execution.

The subagent orchestrator MUST:
- Call `IRunLifecycleManager.StartRunAsync` once per subagent invocation
- Assign a unique RUN ID in format `RUN-{guid}`
- Write all subagent evidence under `.aos/evidence/runs/<run-id>/`
- Close the run record when subagent completes or fails
- Never reuse RUN IDs across different task executions

#### Scenario: Each task gets distinct RUN record
- **GIVEN** two sequential task executions (TSK-0001, TSK-0002)
- **WHEN** both tasks are executed via subagent orchestrator
- **THEN** two distinct `.aos/evidence/runs/RUN-*/` folders exist with different RUN IDs

#### Scenario: Run record contains subagent-specific evidence
- **GIVEN** a completed subagent run
- **WHEN** examining the run folder
- **THEN** it contains `logs/`, `artifacts/`, and `run.json` specific to that subagent execution

### Requirement: Subagent context is fresh and isolated per task
The system SHALL spawn each subagent with fresh, isolated context that does not leak state from previous executions.

The orchestrator MUST:
- Load context from the provided `ContextPackId` only
- Not inherit environment, variables, or state from parent process
- Initialize each subagent with clean working state
- Prevent subagent from accessing files outside `FileScopes`

#### Scenario: Subagent starts with clean state
- **GIVEN** a subagent run request with context pack PCK-0001
- **WHEN** the subagent initializes
- **THEN** it has access only to files in the context pack and allowed file scopes

#### Scenario: Subagent isolation prevents cross-task leakage
- **GIVEN** subagent A executed for TSK-0001 and modified temporary state
- **WHEN** subagent B is spawned for TSK-0002
- **THEN** subagent B cannot see or access any state from subagent A's execution

### Requirement: Context packs are bounded per step
The system SHALL enforce context pack budget boundaries for each subagent execution step.

The orchestrator MUST:
- Accept a context pack with defined byte/token budget
- Not exceed the budget when assembling context for the subagent
- Fail gracefully if the task plan requires more context than the budget allows
- Log budget enforcement decisions to the run evidence

#### Scenario: Context pack respects budget boundary
- **GIVEN** a context pack budget of 100KB
- **WHEN** the pack is built for subagent execution
- **THEN** the pack size does not exceed 100KB and includes artifacts up to that limit

#### Scenario: Budget exceeded is reported deterministically
- **GIVEN** a task plan requiring 200KB of context with 100KB budget
- **WHEN** the subagent is prepared
- **THEN** execution fails with clear error indicating budget exceeded and suggested actions

### Requirement: Subagent evidence is captured comprehensively
The system SHALL capture comprehensive evidence from each subagent execution.

The orchestrator MUST capture:
- All output files written by the subagent
- Complete execution logs (stdout/stderr equivalent)
- Tool call records and LLM interaction logs
- Execution timing and resource usage
- Error traces and stack dumps on failure

All evidence MUST be written to `.aos/evidence/runs/<run-id>/` using deterministic JSON.

#### Scenario: Subagent output files are captured as artifacts
- **GIVEN** a subagent that writes files during execution
- **WHEN** the subagent completes
- **THEN** all written files exist under `.aos/evidence/runs/<run-id>/artifacts/`

#### Scenario: Subagent logs are preserved
- **GIVEN** a subagent execution that produces logs
- **WHEN** execution completes
- **THEN** logs exist under `.aos/evidence/runs/<run-id>/logs/` in deterministic format

### Requirement: Subagent Orchestrator integrates with Task Executor
The system SHALL integrate the subagent orchestrator with the task executor as its execution backend.

The integration MUST:
- Have `TaskExecutor` call `SubagentOrchestrator` for actual plan execution
- Pass context pack references from the task plan
- Receive results and translate to `TaskExecutionResult`
- Handle subagent failures and convert to execution errors

#### Scenario: Task executor delegates to subagent orchestrator
- **GIVEN** a task execution request for TSK-0001
- **WHEN** `TaskExecutor.ExecuteAsync` processes the request
- **THEN** it calls `SubagentOrchestrator.RunSubagentAsync` with appropriate context pack and file scopes

#### Scenario: Subagent failure propagates to task failure
- **GIVEN** a subagent that fails during execution
- **WHEN** `RunSubagentAsync` returns failure
- **THEN** `TaskExecutor` converts the failure to a `TaskExecutionResult` with appropriate error details

