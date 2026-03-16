## 1. Infrastructure and Interfaces

- [x] 1.1 Create `nirmata.Agents/Execution/Execution/TaskExecutor/` directory structure
- [x] 1.2 Define `ITaskExecutor` interface with `ExecuteAsync` method
- [x] 1.3 Define `TaskExecutionRequest` and `TaskExecutionResult` models
- [x] 1.4 Create `nirmata.Agents/Execution/Execution/SubagentRuns/` directory structure
- [x] 1.5 Define `ISubagentOrchestrator` interface with `RunSubagentAsync` method
- [x] 1.6 Define `SubagentRunRequest` and `SubagentRunResult` models

## 2. Task Executor Implementation

- [x] 2.1 Implement `TaskExecutor` class with plan loading from `.aos/spec/tasks/<id>/plan.json`
- [x] 2.2 Implement file scope parsing and validation from `plan.json`
- [x] 2.3 Implement scope enforcement (reject modifications outside allowed files)
- [x] 2.4 Integrate with `IRunLifecycleManager` for run record creation
- [x] 2.5 Implement task evidence pointer updates (`latest.json`)
- [x] 2.6 Implement deterministic state updates (events.ndjson, state.json)
- [x] 2.7 Implement normalized result generation with `NormalizedOutput` and `DeterministicHash`

## 3. Subagent Orchestration Implementation

- [x] 3.1 Implement `SubagentOrchestrator` class with fresh context isolation
- [x] 3.2 Implement context pack loading and budget enforcement
- [x] 3.3 Implement distinct RUN record creation per subagent invocation
- [x] 3.4 Implement subagent evidence capture (logs, artifacts, tool calls)
- [x] 3.5 Implement failure handling and error propagation
- [x] 3.6 Integrate with `ITaskExecutor` as execution backend

## 4. Handler and Orchestrator Integration

- [x] 4.1 Implement `TaskExecutorHandler` following orchestrator handler pattern
- [x] 4.2 Integrate handler with `ICommandRouter` for dispatch
- [x] 4.3 Implement routing logic (success → Verifier, failure → FixPlanner)
- [x] 4.4 Register handler in orchestrator's phase routing table

## 5. Testing and Validation

- [x] 5.1 Write unit tests for `TaskExecutor` scope enforcement
- [x] 5.2 Write unit tests for `SubagentOrchestrator` isolation
- [x] 5.3 Write integration tests for end-to-end task execution flow
- [x] 5.4 Write tests for RUN record creation and evidence capture
- [x] 5.5 Write tests for task evidence pointer updates
- [x] 5.6 Validate deterministic JSON serialization compliance

## 6. Documentation and Examples

- [x] 6.1 Document task plan JSON schema for execution
- [x] 6.2 Document context pack integration patterns
- [x] 6.3 Create example task plan for reference
- [x] 6.4 Update `openspec/specs/` with final spec after implementation
