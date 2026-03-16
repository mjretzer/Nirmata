## ADDED Requirements

### Requirement: Roadmapper interface for roadmap generation

The system SHALL provide an `IRoadmapper` interface in `nirmata.Agents.Execution.Planning.Roadmapper` that generates a complete roadmap from a validated project specification.

The interface MUST define:
- `GenerateRoadmapAsync(RoadmapContext context, CancellationToken ct)` → returns `Task<RoadmapResult>`

`RoadmapContext` MUST carry:
- `RunId` (string): Current run identifier for evidence association
- `WorkspacePath` (string): Absolute path to workspace root
- `ProjectSpec` (object): The validated project specification document

`RoadmapResult` MUST include:
- `IsSuccess` (bool): Whether roadmap generation completed successfully
- `RoadmapSpec` (object|null): The normalized roadmap specification document
- `MilestoneSpecs` (list): Ordered list of milestone specification documents
- `PhaseSpecs` (list): Ordered list of phase specification documents (stubs)
- `Error` (string|null): Error message if generation failed

#### Scenario: Roadmap generation produces valid spec artifacts

- **GIVEN** a workspace with valid `.aos/spec/project.json`
- **WHEN** `GenerateRoadmapAsync` is called with a valid context
- **THEN** the method returns `IsSuccess=true` with `RoadmapSpec` containing `milestones[]` and `phases[]` references

#### Scenario: Roadmap generation fails gracefully on invalid project spec

- **GIVEN** a workspace where the project spec violates schema
- **WHEN** `GenerateRoadmapAsync` is called
- **THEN** the method returns `IsSuccess=false` with a descriptive error indicating schema violation

### Requirement: Roadmap generator creates deterministic milestone skeleton

The system SHALL provide a `RoadmapGenerator` that creates a deterministic milestone structure from project input.

The generator MUST:
- Create exactly one milestone: `MS-0001` (Initial Delivery)
- Assign milestone `name` derived from project name + " Delivery"
- Assign milestone `description` as a synthesis of project description
- Set `schemaVersion` to `1` on milestone documents
- Validate output against `nirmata:aos:schema:milestone:v1` before returning

#### Scenario: Generator produces canonical milestone document

- **GIVEN** a project spec with name "MyApplication"
- **WHEN** `GenerateMilestoneAsync` is called
- **THEN** the output validates against `nirmata:aos:schema:milestone:v1` with `id: "MS-0001"` and `name: "MyApplication Delivery"`

### Requirement: Roadmap generator creates deterministic phase skeletons

The system SHALL generate a deterministic set of phase stubs linked to the milestone.

The generator MUST create phases:
- `PH-0001`: Foundation (setup, infrastructure, boilerplate)
- `PH-0002`: Implementation (core functionality)
- `PH-0003`: Validation (testing, verification, acceptance)

Each phase MUST:
- Set `schemaVersion` to `1`
- Include `milestoneId` referencing `MS-0001`
- Include `sequence` number (1, 2, 3)
- Validate against `nirmata:aos:schema:phase:v1`

#### Scenario: Generator produces three phase stubs in sequence

- **GIVEN** a valid milestone `MS-0001`
- **WHEN** `GeneratePhasesAsync` is called
- **THEN** three phase documents are produced with ids `PH-0001`, `PH-0002`, `PH-0003` and sequential `sequence` values

### Requirement: Roadmap artifacts are written to spec store

The Roadmapper MUST persist all generated artifacts via `ISpecStore` to canonical paths:
- `.aos/spec/roadmap.json` (roadmap catalog document)
- `.aos/spec/milestones/MS-0001/milestone.json`
- `.aos/spec/phases/PH-0001/phase.json`
- `.aos/spec/phases/PH-0002/phase.json`
- `.aos/spec/phases/PH-0003/phase.json`

All writes MUST:
- Use deterministic JSON serialization
- Update corresponding catalog indexes (`milestones/index.json`, `phases/index.json`)
- Be atomic (all succeed or none written)

#### Scenario: Roadmap creation writes all spec artifacts

- **GIVEN** a successful roadmap generation
- **WHEN** artifacts are persisted
- **THEN** all five artifact paths exist with valid JSON and catalog indexes are updated

### Requirement: State cursor is positioned at first phase

After roadmap creation, the system MUST write `.aos/state/state.json` with:
- `cursor.milestoneId` = `"MS-0001"`
- `cursor.milestoneStatus` = `"in_progress"`
- `cursor.phaseId` = `"PH-0001"`
- `cursor.phaseStatus` = `"pending"`
- `cursor.taskId` = `null`
- `cursor.stepId` = `null`

#### Scenario: Cursor positioned at first phase after roadmap creation

- **GIVEN** a successful roadmap generation and persistence
- **WHEN** state is written
- **THEN** `state.json` contains cursor pointing to `PH-0001` with status `pending`

### Requirement: Roadmap creation event is captured

The system SHALL append a `roadmap.created` event to `.aos/state/events.ndjson`.

The event MUST:
- Include `eventType: "roadmap.created"`
- Include `timestampUtc` in ISO 8601 format
- Include `data.roadmapId` referencing the generated roadmap
- Include `data.milestoneCount` and `data.phaseCount`
- Validate against `nirmata:aos:schema:event:v1`

#### Scenario: Event captured on successful roadmap creation

- **GIVEN** a successful roadmap generation
- **WHEN** the workflow completes
- **THEN** `events.ndjson` contains a new line with `eventType: "roadmap.created"`

### Requirement: Roadmapper integrates with orchestrator phase dispatch

The system SHALL provide a `RoadmapperPhaseHandler` implementing the phase handler contract used by the orchestrator.

The handler MUST:
- Accept gating result with `TargetPhase: Roadmapper`
- Delegate to `IRoadmapper.GenerateRoadmapAsync`
- On success: write all spec artifacts, state, and event
- Return a phase result indicating `HasRoadmap: true` for subsequent gating

#### Scenario: Handler executes roadmap generation and updates context

- **GIVEN** a gating result with `TargetPhase: Roadmapper` and `HasProject: true, HasRoadmap: false`
- **WHEN** the handler executes
- **THEN** it generates the roadmap, writes all artifacts, and returns a phase result with `HasRoadmap: true`

#### Scenario: Handler fails gracefully on generation error

- **GIVEN** a gating result with invalid project spec context
- **WHEN** the handler executes
- **THEN** it returns a phase result with `IsSuccess: false` and preserves `HasRoadmap: false`

### Requirement: Roadmap validation passes before state seeding

Before writing state artifacts, the Roadmapper MUST validate:
- All spec artifacts exist at canonical paths
- All spec artifacts validate against their schemas
- Roadmap references resolve to existing milestone/phase artifacts
- Catalog indexes contain the expected IDs

#### Scenario: Validation passes before state is written

- **GIVEN** a successful spec artifact generation
- **WHEN** the Roadmapper prepares to seed state
- **THEN** validation passes confirming all artifacts exist and are schema-compliant

#### Scenario: Validation fails on missing phase artifact

- **GIVEN** a partially written spec (roadmap.json exists but PH-0002 is missing)
- **WHEN** validation is performed
- **THEN** it fails with a descriptive error indicating missing `.aos/spec/phases/PH-0002/phase.json`
