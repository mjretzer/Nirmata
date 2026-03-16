# Change: Add Workflow Classifier for MVP

## Why

The current architecture requires the Windows Service (`nirmata.Windows.Service`) to host and execute agent workflows. For MVP and debugging scenarios, we need a lightweight in-process runner that allows the Web UI to directly invoke agents via the `nirmata.Agents` library without the operational overhead of a separate service process. This enables:

- Faster development iteration and debugging
- Simplified deployment for early adopters
- A unified web interface for both product features and agent observability

## What Changes

- **ADDED**: `nirmata.Web/Composition/` module with `AddnirmataAgents()` integration for direct DI wiring
- **ADDED**: `nirmata.Web/AgentRunner/WorkflowClassifier.cs` - in-process wrapper that calls `nirmata.Agents` orchestrator directly (no HTTP/service boundary)
- **ADDED**: `nirmata.Web/Pages/Runs/Index.cshtml` - runs dashboard listing all runs with status
- **ADDED**: `nirmata.Web/Pages/Runs/Details.cshtml` - run detail page showing status, logs, and artifact pointers
- **ADDED**: New capability specs for `web-agent-runner` and `web-runs-dashboard`
- **MODIFIED**: `web-razor-pages` spec to include navigation integration with Runs dashboard

## Impact

- **Affected specs**: `web-razor-pages`, `orchestrator-workflow`
- **Affected code**:
  - `nirmata.Web/AgentRunner/**` (new)
  - `nirmata.Web/Composition/**` (new)
  - `nirmata.Web/Pages/Runs/**` (new)
  - `nirmata.Web/Pages/Shared/_Layout.cshtml` (navigation link)
- **Dependencies**: Requires `nirmata.Agents` public API (`IOrchestrator`, `WorkflowIntent`, `OrchestratorResult`)
- **No breaking changes**: Windows Service hosts remain fully operational; this is additive

## Notes

- `.aos/*` writes are performed by the Plane (nirmata.Agents) when runs execute — the Product (nirmata.Web) does not write directly to `.aos/*`
- The WorkflowClassifier is a thin wrapper that normalizes inputs and delegates to `IOrchestrator.ExecuteAsync()`
