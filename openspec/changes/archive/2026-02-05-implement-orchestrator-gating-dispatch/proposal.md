# Change: Implement Orchestrator Workflow with Gating and Dispatch

## Why

The agent orchestration layer (`Gmsd.Agents`) needs a control-plane workflow that can classify inputs, enforce gating rules, dispatch to appropriate handlers, and manage the run lifecycle through validation and persistence. Without this, there's no unified way to route agent work based on workspace state (missing project → Interviewer, missing roadmap → Roadmapper, etc.).

This is the core "classify → gate → dispatch → validate → persist → next" loop described in the workflow planes architecture.

## What Changes

- **ADDED** `IOrchestrator` interface and `Orchestrator` implementation in `Gmsd.Agents/Execution/Orchestrator/`
- **ADDED** Gating engine that evaluates workspace state and selects appropriate workflow phase
- **ADDED** Run lifecycle integration with evidence folder creation via injected `IWorkspace`
- **ADDED** Direct service injection pattern: `ICommandRouter`, `IWorkspace`, `ISpecStore`, `IStateStore`, `IValidator`
- **ADDED** Run record management (open → attach input → execute → close status) via `IRunRepository` wrappers
- **ADDED** Workspace outputs under `.aos/evidence/runs/RUN-*/` and `.aos/state/events.ndjson`
- **ADDED** Unit tests covering routing decisions and simulated workspace gating scenarios

## Impact

- **New capability**: `agents-orchestrator-workflow` spec introduced
- **Affected projects**: `Gmsd.Agents` (primary), `Gmsd.Aos` (consumes public APIs)
- **Key files/systems**:
  - `Gmsd.Agents/Execution/Orchestrator/Orchestrator.cs`
  - `Gmsd.Agents/Execution/Orchestrator/GatingEngine.cs`
  - `Gmsd.Agents/Persistence/RunLifecycleManager.cs`
  - `Gmsd.Agents/Persistence/EvidenceFolderManager.cs`
- **Depends on**: Existing `aos-command-routing`, `aos-run-lifecycle`, `aos-state-store` specs
- **No breaking changes**: All new surface area, additive only
