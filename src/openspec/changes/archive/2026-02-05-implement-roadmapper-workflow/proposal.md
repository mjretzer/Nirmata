# Change: Implement Roadmapper Planning Workflow (PH-PLN-0004)

## Why

The orchestrator gating engine requires a Roadmapper phase to transform a validated project specification into a structured execution roadmap. Without this workflow, the system cannot progress from project definition to executable planning phases. The Roadmapper bridges the gap between high-level project intent and concrete milestone/phase/task structures.

## What Changes

- **ADDED** `IRoadmapper` interface and `Roadmapper` implementation in `nirmata.Agents.Execution.Planning.Roadmapper`
- **ADDED** `RoadmapGenerator` for deterministic milestone/phase skeleton generation
- **ADDED** `RoadmapperPhaseHandler` for orchestrator integration
- **ADDED** `RoadmapCreatedEvent` capture to `.aos/state/events.ndjson`
- **ADDED** Spec artifacts: `.aos/spec/roadmap.json`, milestone stubs, phase stubs
- **ADDED** State artifacts: `.aos/state/state.json` with cursor positioned at first phase
- **MODIFIED** Gating engine recognizes `HasRoadmap` context after successful execution

## Impact

- **Affected specs:** `agents-orchestrator-workflow` (gating context), `aos-spec-store` (artifact creation), `aos-state-store` (cursor management), `aos-event-store` (event types)
- **Affected code:**
  - `nirmata.Agents/Execution/Planning/Roadmapper/**` (new)
  - `nirmata.Agents/Execution/Orchestrator/GatingEngine.cs` (context update)
  - `nirmata.Aos/Contracts/State/` (event type catalog)

## Verification

- `openspec validate implement-roadmapper-workflow --strict` passes
- `aos validate spec` passes on generated artifacts
- `aos validate state` passes with correct cursor position
- Event `roadmap.created` appears in `.aos/state/events.ndjson`
