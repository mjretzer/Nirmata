# Change: Implement Phase Planner Workflows

## Why
The orchestrator currently routes to a Planner phase when no plan exists at the cursor, but the actual Phase Context Gatherer, Phase Planner, and Phase Assumption Lister workflows are not implemented. This prevents the system from decomposing roadmap phases into atomic tasks with explicit file scopes and verification steps.

## What Changes
- Add `IPhasePlanner` interface and `PhasePlanner` implementation in `Gmsd.Agents/Execution/Planning/PhasePlanner/`
- Add `IPhaseContextGatherer` interface and implementation in `Gmsd.Agents/Execution/Planning/PhasePlanner/ContextGatherer/`
- Add `IPhaseAssumptionLister` interface and implementation in `Gmsd.Agents/Execution/Planning/PhasePlanner/Assumptions/`
- Add `PhasePlannerHandler` for orchestrator integration
- Implement task decomposition with plan.json generation (file scopes + verification steps)
- Implement phase brief persistence as decisions/events to `.aos/state/events.ndjson`
- Implement assumptions snapshot generation to `.aos/evidence/runs/RUN-*/artifacts/assumptions.md`

## Impact
- **Affected specs:** orchestrator-workflow (gating now routes to working Planner)
- **Affected code:**
  - `Gmsd.Agents/Execution/Planning/PhasePlanner/**` (new)
  - `Gmsd.Agents/Workflows/Planning/` (may add handler integration)
- **New capabilities:** phase-planning
