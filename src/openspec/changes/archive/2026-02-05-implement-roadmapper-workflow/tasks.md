## 1. Implementation

### 1.1 Core Roadmapper Interface and Models
- [x] Create `IRoadmapper` interface in `Workflows/Planning/IRoadmapper.cs`
- [x] Define `RoadmapContext` model in `Models/Runtime/RoadmapContext.cs` (RunId, WorkspacePath, ProjectSpec reference)
- [x] Define `RoadmapResult` model in `Models/Results/RoadmapResult.cs` (IsSuccess, RoadmapSpec, MilestoneSpecs, PhaseSpecs, Error)
- [x] Define `MilestoneItem` model in `Models/Contracts/MilestoneItem.cs` for skeleton generation
- [x] Define `PhaseItem` model in `Models/Contracts/PhaseItem.cs` for skeleton generation

### 1.2 Roadmap Generation Logic
- [x] Implement `RoadmapGenerator` in `Workflows/Planning/RoadmapGenerator.cs`
- [x] Generate default milestone (MS-0001: Initial Delivery)
- [x] Generate default phases (PH-0001: Foundation, PH-0002: Implementation, PH-0003: Validation)
- [x] Map phases to milestone
- [x] Validate generated roadmap against schema `nirmata:aos:schema:roadmap:v1`

### 1.3 Spec Artifact Persistence
- [x] Write `.aos/spec/roadmap.json` via `ISpecStore`
- [x] Write `.aos/spec/milestones/MS-0001/milestone.json`
- [x] Write `.aos/spec/phases/PH-0001/phase.json` (stub)
- [x] Write `.aos/spec/phases/PH-0002/phase.json` (stub)
- [x] Write `.aos/spec/phases/PH-0003/phase.json` (stub)
- [x] Update `.aos/spec/milestones/index.json`
- [x] Update `.aos/spec/phases/index.json`

### 1.4 State Management
- [x] Write `.aos/state/state.json` with cursor at first phase (PH-0001)
- [x] Set `cursor.phaseId = "PH-0001"` and `cursor.phaseStatus = "pending"`
- [x] Ensure state validates against `nirmata:aos:schema:state:v1`

### 1.5 Event Capture
- [x] Append `roadmap.created` event to `.aos/state/events.ndjson`
- [x] Event MUST include `eventType: "roadmap.created"`, `timestampUtc`, and `data.roadmapId`
- [x] Ensure event validates against `nirmata:aos:schema:event:v1`

### 1.6 Orchestrator Integration
- [x] Create `RoadmapperHandler` in `Workflows/Planning/RoadmapperHandler.cs`
- [x] Register handler in `Execution/ControlPlane/` orchestrator dispatch table
- [x] Update gating context with `HasRoadmap: true` on success

## 2. Validation

- [x] Unit tests: `RoadmapGeneratorTests` in `tests/nirmata.Agents.Tests/Workflows/Planning/RoadmapGeneratorTests.cs`
- [x] Unit tests: `RoadmapperTests` in `tests/nirmata.Agents.Tests/Workflows/Planning/RoadmapperTests.cs`
- [x] Integration tests: Full workflow with temp workspace in `tests/nirmata.Agents.Tests/Workflows/Planning/RoadmapperIntegrationTests.cs`
- [x] Verify `aos validate spec` passes on generated artifacts
- [x] Verify `aos validate state` passes with cursor positioned at PH-0001
- [x] Verify event appears in `events.ndjson` with correct type

## 3. Documentation

- [x] XML documentation on public interfaces
- [x] README.md in `nirmata.Agents/Workflows/Planning/Roadmapper/`
- [x] Update `openspec/specs/agents-roadmapper-workflow/spec.md` with final implementation notes
