# Tasks: Refine Orchestrator Flow

## 1. Core Component Refactoring

- [x] Rename `DirectAgentRunner` to `WorkflowClassifier` or a similar name that reflects its role.
- [x] Refactor `GatingEngine` to ensure its rules strictly match the documented state machine.
- [x] Enhance `RunLifecycleManager` to explicitly manage `aos run start` and `aos run finish` states.

## 2. Introduce Validation Steps

- [x] Create a `PreflightValidator` service and integrate it into the orchestrator before the gating step.
- [x] Implement an `OutputValidator` to check specialist handler results before persisting them.

## 3. Implement Subagent Orchestration

- [x] Create a `SubagentOrchestrator` responsible for isolated plan step execution.
- [x] Refactor the `Executor` phase to dispatch to the `SubagentOrchestrator`.

## 4. Update Orchestrator Control Loop

- [x] Modify `Orchestrator.ExecuteAsync` to follow the full `classify -> load -> validate -> gate -> dispatch -> validate -> persist -> return` sequence.
- [x] Integrate context pack generation into the dispatch step.

## 5. Documentation and Validation

- [x] Update all relevant code comments and documentation to reflect the new flow and component names.
- [x] Create or update integration tests to verify the end-to-end flow.
- [x] Run `openspec validate refine-orchestrator-flow --strict` to ensure compliance.
