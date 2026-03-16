# Change: Implement Real Subagent Loop Execution

## Why
The subagent orchestrator currently contains placeholder execution code (`ExecuteSubagentLogicAsync`) that simulates execution instead of using the real tool-calling loop implementation. While the real implementation exists (`ExecuteSubagentLogicWithBudgetAsync`), the placeholder code remains in the codebase creating confusion and potential for accidental regression.

## What Changes
- Remove the placeholder `ExecuteSubagentLogicAsync` method from `SubagentOrchestrator.cs`
- Ensure all subagent execution uses the real `IToolCallingLoop` integration
- Add comprehensive subagent prompt template with bounded context, task plan, file scopes, and verification requirements
- Implement complete tool set: file read/write (scoped), process runner (tests/build), git status/commit (if enabled)
- Add budget controller enforcement: max iterations, max tool calls, max tokens, wall-clock timeout
- Enhance evidence capture: tool calls, diffs, command outputs, final summary hash and deterministic outputs

## Impact
- Affected specs: agents-subagent-orchestration
- Affected code: `nirmata.Agents/Execution/Execution/SubagentRuns/SubagentOrchestrator.cs`
- **BREAKING**: Removes unused placeholder method that may be referenced in tests
