## 1. Interfaces and Contracts
- [x] 1.1 Create `IPhaseContextGatherer` interface in `nirmata.Agents/Execution/Planning/PhasePlanner/ContextGatherer/`
- [x] 1.2 Create `IPhasePlanner` interface in `nirmata.Agents/Execution/Planning/PhasePlanner/`
- [x] 1.3 Create `IPhaseAssumptionLister` interface in `nirmata.Agents/Execution/Planning/PhasePlanner/Assumptions/`
- [x] 1.4 Create data models: `PhaseBrief`, `TaskSpecification`, `TaskPlan`, `PlanningDecision`
- [x] 1.5 Create `PhasePlannerHandler` interface and implementation

## 2. Phase Context Gatherer Implementation
- [x] 2.1 Implement `PhaseContextGatherer` class
- [x] 2.2 Add roadmap.json reader to extract phase specifications
- [x] 2.3 Add project.json context collection
- [x] 2.4 Add codebase intelligence gathering (relevant files)
- [x] 2.5 Implement `PhaseBrief` generation with goals, constraints, scope
- [x] 2.6 Implement decision/event persistence to `.aos/state/events.ndjson`
- [x] 2.7 Write unit tests for context gatherer

## 3. Phase Planner Implementation
- [x] 3.1 Implement `PhasePlanner` class with LLM integration
- [x] 3.2 Create structured output schema for task decomposition
- [x] 3.3 Implement task ID generation (TSK-XXXX format)
- [x] 3.4 Implement task.json writer to `.aos/spec/tasks/{id}/task.json`
- [x] 3.5 Implement plan.json writer with file scopes and verification checks
- [x] 3.6 Implement links.json writer for task relationships
- [x] 3.7 Add validation: enforce 2-3 task limit per phase
- [x] 3.8 Write unit tests for phase planner

## 4. Phase Assumption Lister Implementation
- [x] 4.1 Implement `PhaseAssumptionLister` class
- [x] 4.2 Add assumption extraction from phase brief and task plans
- [x] 4.3 Implement assumptions.md generation
- [x] 4.4 Integrate with evidence system for artifact attachment
- [x] 4.5 Write unit tests for assumption lister

## 5. Handler and Orchestrator Integration
- [x] 5.1 Implement `PhasePlannerHandler` coordinating gatherer → planner → lister
- [x] 5.2 Wire handler into orchestrator gating system
- [x] 5.3 Add integration with `IRunLifecycleManager` for evidence capture
- [x] 5.4 Add error handling and rollback on failure
- [x] 5.5 Write integration tests for full planning workflow

## 6. Validation and Verification
- [x] 6.1 Run `openspec validate implement-phase-planner-workflows --strict`
- [x] 6.2 Verify handler integration with orchestrator
- [x] 6.3 End-to-end test: project → roadmap → phase → tasks
- [x] 6.4 Verify artifacts created in correct locations
- [x] 6.5 Verify state events contain planning decisions
