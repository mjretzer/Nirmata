## 1. Core Orchestrator Implementation
- [x] 1.1 Create `IOrchestrator` interface with `ExecuteAsync` method accepting workflow intent
- [x] 1.2 Create `Orchestrator` class implementing the interface
- [x] 1.3 Define `WorkflowIntent` model (normalized CLI/freeform input)
- [x] 1.4 Define `OrchestratorResult` model (success/failure, next phase, artifacts)

## 2. Gating Engine
- [x] 2.1 Create `IGatingEngine` interface
- [x] 2.2 Create `GatingEngine` implementation evaluating workspace state
- [x] 2.3 Implement gating rules:
  - Missing project → `Interviewer` phase
  - Missing roadmap → `Roadmapper` phase  
  - Missing plan → `Planner` phase
  - Has plan → `Executor` phase
  - Post-execution → `Verifier` phase
  - Verification fail → `FixPlanner` phase
- [x] 2.4 Create `GatingContext` model carrying workspace state snapshot

## 3. Service Integration
- [x] 3.1 Wire up DI registration for `IOrchestrator` and `IGatingEngine`
- [x] 3.2 Inject `ICommandRouter` for command dispatch
- [x] 3.3 Inject `IWorkspace` for path resolution
- [x] 3.4 Inject `ISpecStore` for reading project/roadmap/plan state
- [x] 3.5 Inject `IStateStore` for cursor and event operations
- [x] 3.6 Inject `IValidator` for workspace validation

## 4. Run Lifecycle Integration
- [x] 4.1 Create `IRunLifecycleManager` interface
- [x] 4.2 Create `RunLifecycleManager` wrapping `IRunRepository` with Engine stores
- [x] 4.3 Implement `StartRunAsync` - creates run, opens evidence folder
- [x] 4.4 Implement `AttachInputAsync` - records normalized input to run
- [x] 4.5 Implement `FinishRunAsync` - finalizes status, writes summary
- [x] 4.6 Create `IEvidenceFolderManager` for `.aos/evidence/runs/RUN-*/` structure

## 5. Workspace Outputs
- [x] 5.1 Write `commands.json` recording dispatched commands per run
- [x] 5.2 Write `summary.json` with run metadata and artifact pointers
- [x] 5.3 Append run lifecycle events to `.aos/state/events.ndjson`
- [x] 5.4 Create `logs/` and `artifacts/` subdirectories per run

## 6. Testing
- [x] 6.1 Unit tests for `GatingEngine` routing decisions
- [x] 6.2 Unit tests for `Orchestrator` workflow loop
- [x] 6.3 Simulated workspace tests hitting each gating gate correctly
- [x] 6.4 Evidence folder creation verification tests
- [x] 6.5 Integration tests with fake/stub Engine stores

## 7. Validation
- [x] 7.1 Run `openspec validate implement-orchestrator-gating-dispatch --strict`
- [x] 7.2 Ensure all tests pass
- [x] 7.3 Verify no breaking changes to existing AOS public APIs
