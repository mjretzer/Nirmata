# agents-orchestrator-workflow Specification

## Purpose

Defines orchestration-plane workflow semantics for $capabilityId.

- **Lives in:** `Gmsd.Agents/*`
- **Owns:** Control-plane routing/gating and workflow orchestration for this capability
- **Does not own:** Engine contract storage/serialization and product domain behavior
## Requirements
### Requirement: Orchestrator provides unified workflow execution entry point

The orchestrator SHALL follow a strict, observable, and validatable control loop for every execution, including classification, validation, gating, dispatch, and persistence.

#### Scenario: A user provides freeform input
When a user provides freeform text, the system SHALL execute the full control loop, from classification to persistence, and return a structured result.

### Requirement: Gating engine evaluates workspace state and selects phase

The system SHALL provide an `IGatingEngine` interface in `Gmsd.Agents.Execution.Orchestrator` that evaluates workspace state and determines the appropriate workflow phase.

The gating engine MUST implement the following decision logic in priority order:
1. Missing project spec (`.aos/spec/project.json`) â†’ route to **Interviewer**
2. Missing roadmap (`.aos/spec/roadmap.json` or equivalent) â†’ route to **Roadmapper**
3. Missing phase plan for current cursor â†’ route to **Planner**
4. Current cursor indicates execution needed â†’ route to **Executor**
5. Execution completed, verification pending â†’ route to **Verifier**
6. Verification failed â†’ route to **FixPlanner**

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

The system SHALL provide an `IRunLifecycleManager` interface in `Gmsd.Agents.Persistence` that wraps Engine stores to manage the run lifecycle.

The interface MUST define:
- `StartRunAsync(CancellationToken ct)` â†’ returns `RunContext` with new RunId
- `AttachInputAsync(string runId, WorkflowIntent intent, CancellationToken ct)` â†’ records input to run
- `FinishRunAsync(string runId, bool success, Dictionary<string,object>? outputs, CancellationToken ct)` â†’ finalizes run

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

### Requirement: Orchestrator performs deterministic workspace-state startup hook before dispatch
Before workflow phase dispatch, the orchestrator SHALL invoke `EnsureWorkspaceInitialized()` to validate and repair baseline workspace state artifacts deterministically.

`EnsureWorkspaceInitialized()` MUST run before any write-phase dispatch and before run evidence finalization that depends on state snapshot availability.

#### Scenario: Initialization succeeds and write-phase dispatch continues
- **GIVEN** a write-capable request that would route to Planner or Executor
- **WHEN** orchestrator execution begins
- **THEN** it invokes `EnsureWorkspaceInitialized()` before phase handler dispatch
- **AND** phase dispatch proceeds only after readiness succeeds

#### Scenario: Healthy workspace incurs no-op startup behavior
- **GIVEN** a workspace with valid baseline state artifacts already present
- **WHEN** orchestrator invokes startup readiness
- **THEN** readiness completes without mutating baseline state files
- **AND** orchestrator continues through normal gating and dispatch

### Requirement: Orchestrator converts unrecoverable state readiness failures into conversational output
If startup readiness cannot establish deterministic state prerequisites, the orchestrator SHALL stop workflow execution and emit a conversational recovery response with a suggested fix action.

The conversational response MUST include:
- a clear explanation of the failed preflight readiness condition
- a suggested corrective command or action
- an explicit statement that no workflow phase dispatch was performed

#### Scenario: Initialization fails and write-phase dispatch is blocked
- **GIVEN** startup readiness fails because state derivation cannot complete
- **WHEN** orchestrator handles the preflight result
- **THEN** it emits assistant-facing conversational output describing the failure and suggested remediation
- **AND** it does not dispatch any write-phase workflow handler
- **AND** it records the preflight failure outcome in run diagnostics when a run context exists

