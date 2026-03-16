# Change: add-fix-planner-workflow

## Why
The agent orchestration system needs a Fix Planner workflow to consume UAT verification failures and generate targeted fix plans. When the UAT Verifier detects issues, the system must analyze failures, determine scope, and produce 2-3 concrete fix task plans with explicit verification steps. This closes the verification-fix loop in the execution pipeline.

## What Changes
- **ADD** `IFixPlanner` interface and implementation in `nirmata.Agents.Execution.FixPlanner`
- **ADD** `FixPlannerHandler` integrating with orchestrator control plane
- **ADD** Fix task plan generation producing `TSK-*/plan.json` with explicit scope + verification checks
- **ADD** Gating engine routing from `Verifier` phase to `FixPlanner` on failure
- **ADD** State management for `ready-to-execute-fix` cursor indication

## Impact
- **Affected specs:** agents-fix-planner (new), agents-uat-verifier (reference), agents-task-executor (consumer)
- **Affected code:**
  - `nirmata.Agents/Execution/FixPlanner/**` (new)
  - `nirmata.Agents/Execution/ControlPlane/FixPlannerHandler.cs` (new)
  - `nirmata.Agents/Execution/ControlPlane/GatingEngine.cs` (MODIFIED for routing)
- **Workspace outputs:**
  - Input: `.aos/spec/issues/ISS-*.json` (from UAT Verifier)
  - Output: `.aos/spec/tasks/TSK-*/plan.json`, `.aos/spec/tasks/TSK-*/task.json`, `.aos/spec/tasks/TSK-*/links.json`
  - State: `.aos/state/state.json` (cursor), `.aos/state/events.ndjson` (events)

## Breaking Changes
None. This is additive functionality that consumes existing issue artifacts.
