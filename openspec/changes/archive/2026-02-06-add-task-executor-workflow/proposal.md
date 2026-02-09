# Change: Add Task Executor Workflow

## Why
The Execution Plane needs a deterministic Task Executor that can execute task plans sequentially with strict file-scoping, per-task subagent isolation, and comprehensive evidence capture. Currently, the orchestrator can route to an Executor phase, but there is no concrete implementation that handles task-level execution with proper scope enforcement, subagent lifecycle management, and evidence recording.

This change implements the Task Executor workflow that reads task plans from `.aos/spec/tasks/TSK-*/plan.json`, applies changes strictly within allowed file scopes, produces normalized results, updates cursor/task status deterministically, and creates distinct RUN records per atomic task/step.

## What Changes
- **ADDED** `ITaskExecutor` interface and `TaskExecutor` implementation in `Gmsd.Agents.Execution.Execution.TaskExecutor`
- **ADDED** `ISubagentOrchestrator` interface and implementation in `Gmsd.Agents.Execution.Execution.SubagentRuns`
- **ADDED** Task-scoped file enforcement (rejects changes outside allowed scope)
- **ADDED** Per-task subagent spawning with fresh context
- **ADDED** Task evidence capture with `latest.json` pointer updates
- **ADDED** Deterministic cursor and task status updates
- **ADDED** Distinct RUN record creation per atomic task/step
- **ADDED** Integration with `aos-task-evidence` for task completion tracking
- **ADDED** Handler integration with orchestrator's gating system

## Impact
- **Affected specs:** `agents-task-executor` (new), `agents-subagent-orchestration` (new)
- **Affected code:** 
  - `Gmsd.Agents/Execution/Execution/TaskExecutor/**`
  - `Gmsd.Agents/Execution/Execution/SubagentRuns/**`
- **Workspace outputs:**
  - `.aos/evidence/runs/RUN-*/` — per-task run records
  - `.aos/evidence/task-evidence/TSK-*/latest.json` — task completion pointers
  - `.aos/state/state.json` — cursor/task status updates
  - `.aos/state/events.ndjson` — execution events
