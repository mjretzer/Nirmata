# orchestrator-workflow Specification

## Purpose
TBD - created by archiving change implement-orchestrator-gating-dispatch. Update Purpose after archive.
## Requirements
### Requirement: Orchestrator implements "classify → gate → dispatch → validate → persist → next" workflow

The system SHALL provide an `IOrchestrator` interface in `Gmsd.Agents.Execution.Orchestrator` that serves as the unified entry point for agent workflow execution.

The implementation MUST:
- Accept `WorkflowIntent` via `ExecuteAsync` method
- Start run lifecycle via `IRunLifecycleManager`
- Build gating context from workspace state
- Evaluate gates via `IGatingEngine`
- Dispatch to phase handlers via `ICommandRouter`
- Close run with status and outputs
- Return `OrchestratorResult` with success/failure, final phase, run ID, and artifacts

#### Scenario: Orchestrator executes full workflow loop
- **GIVEN** a workspace with valid project, roadmap, and plan
- **WHEN** `ExecuteAsync` is called with a workflow intent
- **THEN** the orchestrator starts run → attaches input → evaluates gates → dispatches to Executor → closes run → returns success result

#### Scenario: Orchestrator handles missing project gate
- **GIVEN** a workspace without a project spec
- **WHEN** `ExecuteAsync` is called with any intent
- **THEN** gating routes to Interviewer and returns result indicating missing project

### Requirement: Gating engine evaluates 6-phase routing logic
The system SHALL provide an `IGatingEngine` interface that evaluates workspace state in priority order:
1. Missing project spec → route to **Interviewer**
2. Missing roadmap → route to **Roadmapper**
3. Missing phase plan → route to **Planner** (now implemented)
4. Ready to execute → route to **Executor**
5. Execution complete, verification pending → route to **Verifier**
6. Verification failed → route to **FixPlanner**

#### Scenario: Gating routes to Planner when plan missing
- **GIVEN** a workspace with project spec and roadmap, but no tasks planned for current phase at cursor
- **WHEN** `EvaluateAsync` is called
- **THEN** result indicates `TargetPhase: Planner` with reason "No plan exists for current cursor position" and routes to working PhasePlannerHandler

### Requirement: Orchestrator uses direct service injection (no CLI spawning)

The `Orchestrator` class MUST accept via constructor injection:
- `ICommandRouter` for dispatching commands
- `IWorkspace` for path resolution
- `ISpecStore` for reading specifications
- `IStateStore` for cursor and state
- `IValidator` for workspace validation
- `IRunLifecycleManager` for run lifecycle

All service calls MUST be direct method invocations, NOT CLI subprocess execution.

#### Scenario: Orchestrator dispatches via injected router
- **GIVEN** an orchestrator with injected `ICommandRouter` mock
- **WHEN** dispatch is required
- **THEN** it calls `router.RouteAsync()` directly without spawning processes

### Requirement: Run lifecycle manager creates evidence folder structure

The system SHALL provide an `IRunLifecycleManager` that manages run lifecycle with evidence folder creation:

Interface methods:
- `StartRunAsync()` → creates `.aos/evidence/runs/RUN-{id}/` folder with `run.json`, `logs/`, `artifacts/`
- `AttachInputAsync()` → writes `input.json` to run folder
- `RecordCommandAsync()` → records command to in-memory log
- `FinishRunAsync()` → writes `commands.json`, `summary.json`, updates `run.json`

#### Scenario: StartRun creates canonical evidence folder
- **GIVEN** an initialized AOS workspace
- **WHEN** `StartRunAsync` is called
- **THEN** folder `.aos/evidence/runs/RUN-{id}/` created with `run.json`, `logs/`, `artifacts/`

#### Scenario: CloseRun writes summary and commands
- **GIVEN** an active run
- **WHEN** `FinishRunAsync` is called
- **THEN** `commands.json` and `summary.json` written to run folder with status and artifacts

### Requirement: Evidence folder follows canonical layout

Each run folder MUST contain:
- `run.json` - metadata (schemaVersion, runId, status, timestamps)
- `commands.json` - record of commands dispatched
- `summary.json` - final summary with artifact pointers
- `logs/` - directory for log files
- `artifacts/` - directory for output artifacts

All JSON files MUST use deterministic serialization (UTF-8, LF endings, stable key ordering).

#### Scenario: Evidence folder has canonical structure
- **GIVEN** a new run started
- **WHEN** folder is created
- **THEN** all required files and directories exist in canonical layout

