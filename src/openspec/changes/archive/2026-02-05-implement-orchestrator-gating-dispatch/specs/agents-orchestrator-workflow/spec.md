## ADDED Requirements

### Requirement: Orchestrator provides unified workflow execution entry point

The system SHALL provide an `IOrchestrator` interface in `nirmata.Agents.Execution.Orchestrator` that serves as the unified entry point for agent workflow execution.

The interface MUST define:
- `ExecuteAsync(WorkflowIntent intent, CancellationToken ct)` method returning `Task<OrchestratorResult>`

`WorkflowIntent` MUST carry:
- `InputRaw` (string): Original user input (CLI args or freeform text)
- `InputNormalized` (string|null): Classified/normalized command representation
- `CorrelationId` (string): Tracing identifier

`OrchestratorResult` MUST include:
- `IsSuccess` (bool): Whether the orchestration completed successfully
- `FinalPhase` (string|null): The last phase that executed (e.g., "Executor", "Verifier")
- `RunId` (string|null): Identifier for the run record created
- `Artifacts` (dictionary): Pointers to evidence artifacts produced

#### Scenario: Orchestrator executes full workflow loop
- **GIVEN** a workspace with a valid project, roadmap, and plan
- **WHEN** `ExecuteAsync` is called with a workflow intent to execute the plan
- **THEN** the orchestrator classifies the intent, evaluates gates, dispatches to Executor, validates results, persists evidence, and returns a successful result with the RunId

#### Scenario: Orchestrator handles missing project gate
- **GIVEN** a workspace without a project spec
- **WHEN** `ExecuteAsync` is called with any intent
- **THEN** the gating engine routes to the Interviewer phase and returns a result indicating missing project context

### Requirement: Gating engine evaluates workspace state and selects phase

The system SHALL provide an `IGatingEngine` interface in `nirmata.Agents.Execution.Orchestrator` that evaluates workspace state and determines the appropriate workflow phase.

The gating engine MUST implement the following decision logic in priority order:
1. Missing project spec (`.aos/spec/project.json`) → route to **Interviewer**
2. Missing roadmap (`.aos/spec/roadmap.json` or equivalent) → route to **Roadmapper**
3. Missing phase plan for current cursor → route to **Planner**
4. Current cursor indicates execution needed → route to **Executor**
5. Execution completed, verification pending → route to **Verifier**
6. Verification failed → route to **FixPlanner**

The engine MUST return a `GatingResult` containing:
- `TargetPhase` (string): One of the six phase names above
- `Reason` (string): Human-readable explanation for the routing decision
- `ContextData` (dictionary): Relevant workspace state snapshots for the handler

#### Scenario: Gating routes to Interviewer when project missing
- **GIVEN** an initialized workspace with no `.aos/spec/project.json`
- **WHEN** `EvaluateAsync` is called on the gating engine
- **THEN** the result indicates `TargetPhase: Interviewer` with reason "Project specification not found"

#### Scenario: Gating routes to Roadmapper when roadmap missing
- **GIVEN** a workspace with project spec but no roadmap artifact
- **WHEN** `EvaluateAsync` is called on the gating engine
- **THEN** the result indicates `TargetPhase: Roadmapper` with reason "Roadmap not defined for project"

#### Scenario: Gating routes to Executor when plan exists
- **GIVEN** a workspace with project, roadmap, and a plan at the current cursor position
- **WHEN** `EvaluateAsync` is called on the gating engine
- **THEN** the result indicates `TargetPhase: Executor` with reason "Ready to execute task plan"

#### Scenario: Gating routes to Verifier after execution
- **GIVEN** a workspace where Executor phase has just completed
- **WHEN** `EvaluateAsync` is called on the gating engine
- **THEN** the result indicates `TargetPhase: Verifier` with reason "Execution complete, awaiting verification"

#### Scenario: Gating routes to FixPlanner on verification failure
- **GIVEN** a workspace where Verifier phase returned failure status
- **WHEN** `EvaluateAsync` is called on the gating engine
- **THEN** the result indicates `TargetPhase: FixPlanner` with reason "Verification failed, fix planning required"

### Requirement: Orchestrator uses direct service injection

The system SHALL implement the orchestrator using direct service injection (DI) rather than process spawning or CLI invocation.

The `Orchestrator` class MUST accept via constructor injection:
- `ICommandRouter` for dispatching commands to handlers
- `IWorkspace` for path resolution and workspace access
- `ISpecStore` for reading project/roadmap/plan specifications
- `IStateStore` for cursor and event operations
- `IValidator` for workspace validation
- `IRunLifecycleManager` for run record management

All service calls MUST be direct method invocations, NOT CLI subprocess execution.

#### Scenario: Orchestrator dispatches via injected router
- **GIVEN** an orchestrator instance with injected `ICommandRouter` mock
- **WHEN** the orchestrator determines a command needs dispatch
- **THEN** it calls `router.RouteAsync()` directly without spawning any process

#### Scenario: Orchestrator reads workspace state via injected stores
- **GIVEN** an orchestrator instance with injected `ISpecStore` and `IStateStore` mocks
- **WHEN** the orchestrator evaluates gating conditions
- **THEN** it calls store methods directly to read project and cursor state

### Requirement: Run lifecycle manager creates and manages run records

The system SHALL provide an `IRunLifecycleManager` interface in `nirmata.Agents.Persistence` that wraps Engine stores to manage the run lifecycle.

The interface MUST define:
- `StartRunAsync(CancellationToken ct)` → returns `RunContext` with new RunId
- `AttachInputAsync(string runId, WorkflowIntent intent, CancellationToken ct)` → records input to run
- `FinishRunAsync(string runId, bool success, Dictionary<string,object>? outputs, CancellationToken ct)` → finalizes run

The implementation MUST:
- Create evidence folder structure under `.aos/evidence/runs/RUN-*/`
- Write `run.json` metadata using deterministic JSON
- Append lifecycle events to `.aos/state/events.ndjson`
- Maintain run index at `.aos/evidence/runs/index.json`

#### Scenario: StartRun creates evidence folder
- **GIVEN** an initialized AOS workspace
- **WHEN** `StartRunAsync` is called
- **THEN** a folder `.aos/evidence/runs/RUN-{id}/` is created with `run.json`, `logs/`, and `artifacts/` subdirectories

#### Scenario: CloseRun writes summary
- **GIVEN** an active run started with `StartRunAsync`
- **WHEN** `FinishRunAsync` is called with success=true
- **THEN** `summary.json` is written to the run folder with status, timestamps, and artifact pointers

### Requirement: Evidence folder structure follows canonical layout

The system SHALL create evidence folders under `.aos/evidence/runs/RUN-*/` following the canonical structure defined by `aos-run-lifecycle`.

Each run folder MUST contain:
- `run.json` - run metadata (schemaVersion, runId, status, timestamps)
- `commands.json` - record of commands dispatched during the run
- `summary.json` - final summary with artifact pointers
- `logs/` - directory for log files
- `artifacts/` - directory for output artifacts

All JSON files MUST be written using deterministic JSON serialization (UTF-8, LF endings, stable key ordering).

#### Scenario: Evidence folder has canonical structure
- **GIVEN** a new run started via `StartRunAsync`
- **WHEN** the evidence folder is created
- **THEN** it contains all required files and directories in the canonical layout

#### Scenario: Commands.json records dispatched commands
- **GIVEN** an orchestrator run that dispatched 3 commands
- **WHEN** the run completes
- **THEN** `commands.json` contains entries for each command with group, command, timestamp, and status
