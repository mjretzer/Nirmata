## 1. Core Interface and Models
- [x] 1.1 Define `IFixPlanner` interface in `nirmata.Agents.Execution.FixPlanner`
- [x] 1.2 Define `FixPlannerRequest` record with IssueIds, WorkspaceRoot, ParentTaskId, ContextPackId
- [x] 1.3 Define `FixPlannerResult` record with IsSuccess, FixTaskIds, IssueAnalysis, ErrorMessage
- [x] 1.4 Define `IssueAnalysis` model with IssueId, RootCause, AffectedFiles, RecommendedFixes

## 2. Fix Planner Implementation
- [x] 2.1 Implement `FixPlanner` class with dependency on `ISpecStore`, `IStateStore`, `ICodebaseContext`
- [x] 2.2 Implement issue loading from `.aos/spec/issues/ISS-*.json`
- [x] 2.3 Implement scope consolidation logic for overlapping issues
- [x] 2.4 Implement task ID generation (deterministic based on parent task + sequence)
- [x] 2.5 Implement fix task plan generation with fileScopes and acceptanceCriteria
- [x] 2.6 Write unit tests for FixPlanner core logic

## 3. Artifact Writing
- [x] 3.1 Implement `task.json` writer with metadata (id, type, status, parentTaskId, issueIds)
- [x] 3.2 Implement `plan.json` writer with steps, fileScopes, acceptanceCriteria
- [x] 3.3 Implement `links.json` writer with parent task and issue references
- [x] 3.4 Ensure deterministic JSON serialization per `aos-deterministic-json-serialization`
- [x] 3.5 Write tests for artifact writing with schema validation

## 4. State Management
- [x] 4.1 Implement cursor update to `FixPlannerComplete` phase on success
- [x] 4.2 Implement context preservation (roadmap/phase position)
- [x] 4.3 Implement `events.ndjson` appending for fix planning lifecycle
- [x] 4.4 Write tests for state transitions and event recording

## 5. Handler Integration
- [x] 5.1 Implement `FixPlannerHandler` in `nirmata.Agents.Execution.ControlPlane`
- [x] 5.2 Implement handler pattern with `HandleAsync` method
- [x] 5.3 Implement routing result to `TaskExecutor` phase on success
- [x] 5.4 Write tests for handler integration with orchestrator

## 6. Gating Engine Integration
- [x] 6.1 Update `IGatingEngine.EvaluateAsync` to detect verification failure state
- [x] 6.2 Implement routing from `Verifier` to `FixPlanner` on failure
- [x] 6.3 Include issue IDs and parent task ID in context data
- [x] 6.4 Write integration tests for gating engine routing

## 7. Verification and Validation
- [x] 7.1 Run `openspec validate add-fix-planner-workflow --strict`
- [x] 7.2 Verify spec passes all validation rules
- [x] 7.3 Create sample artifacts (input issue, output fix task) for manual testing
- [x] 7.4 Document example workflow: failed UAT → FixPlanner → fix tasks ready
