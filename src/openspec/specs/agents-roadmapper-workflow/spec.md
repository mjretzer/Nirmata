# agents-roadmapper-workflow Specification

## Purpose
Transforms a validated project specification into a structured execution roadmap with milestones, phases, and associated artifacts. The Roadmapper workflow bridges the gap between high-level project intent and concrete milestone/phase/task structures required for execution.

## Requirements

### Requirement: Roadmapper interface contract

The system SHALL provide an `IRoadmapper` interface in `nirmata.Agents.Workflows.Planning` that transforms a validated project specification into a structured execution roadmap.

The interface MUST define:
- `GenerateRoadmapAsync(RoadmapContext context, CancellationToken ct)` → returns `Task<RoadmapResult>`

`RoadmapContext` MUST carry:
- `RunId` (string): Current run identifier
- `WorkspacePath` (string): Absolute path to workspace root
- `ProjectSpec` (ProjectSpecReference): Reference to validated project spec with SpecPath, ProjectId, ProjectName, SchemaVersion
- `CorrelationId` (string): Optional correlation ID for tracing
- `Metadata` (Dictionary<string,string>): Optional metadata for the generation

`RoadmapResult` MUST include:
- `IsSuccess` (bool): Whether roadmap generation succeeded
- `RoadmapId` (string): Unique identifier for the generated roadmap
- `RoadmapSpecPath` (string): Path to the generated roadmap spec file
- `MilestoneSpecs` (List<MilestoneSpec>): Generated milestone specifications
- `PhaseSpecs` (List<PhaseSpec>): Generated phase specifications
- `Error` (string|null): Error message if generation failed
- `StartedAt` (DateTimeOffset): Generation start timestamp
- `CompletedAt` (DateTimeOffset): Generation completion timestamp

#### Scenario: Roadmap generation succeeds with default structure

- **GIVEN** a workspace with a valid `.aos/spec/project.json`
- **WHEN** `GenerateRoadmapAsync` is called with a valid context
- **THEN** the method returns `IsSuccess=true` with milestone MS-0001 and phases PH-0001 through PH-0003

#### Scenario: Roadmap generation fails gracefully on invalid project spec

- **GIVEN** a workspace with an invalid or missing project spec
- **WHEN** `GenerateRoadmapAsync` is called
- **THEN** the method returns `IsSuccess=false` with a descriptive error message

### Requirement: Roadmap generator creates deterministic skeleton

The system SHALL provide an `IRoadmapGenerator` interface that generates deterministic milestone and phase skeletons.

The generator MUST:
- Generate milestone MS-0001: "Initial Delivery" with completion criteria
- Generate phases PH-0001 (Foundation), PH-0002 (Implementation), PH-0003 (Validation)
- Map all phases to milestone MS-0001
- Validate generated roadmap against schema `nirmata:aos:schema:roadmap:v1`

`MilestoneItem` MUST include:
- `MilestoneId` (string): Unique identifier (e.g., MS-0001)
- `Name` (string): Human-readable name
- `Description` (string): Description of objectives
- `Phases` (List<PhaseItem>): Associated phases
- `SequenceOrder` (int): Execution order
- `CompletionCriteria` (List<string>): Criteria for milestone completion

`PhaseItem` MUST include:
- `PhaseId` (string): Unique identifier (e.g., PH-0001)
- `Name` (string): Human-readable name
- `Description` (string): Description of objectives
- `MilestoneId` (string): Parent milestone identifier
- `SequenceOrder` (int): Order within milestone
- `Deliverables` (List<string>): Expected deliverables
- `InputArtifacts` (List<string>): Required input artifacts
- `OutputArtifacts` (List<string>): Produced output artifacts

#### Scenario: Generator produces default milestone and phases

- **GIVEN** a request to generate milestones
- **WHEN** `GenerateMilestones` is called
- **THEN** it returns a single milestone MS-0001 with three phases (PH-0001, PH-0002, PH-0003) in correct sequence order

#### Scenario: Schema validation passes for generated roadmap

- **GIVEN** a generated roadmap data structure
- **WHEN** `ValidateAgainstSchema` is called
- **THEN** it returns true with no errors for valid roadmap data

#### Scenario: Schema validation fails for invalid roadmap

- **GIVEN** roadmap data missing required schema or milestones
- **WHEN** `ValidateAgainstSchema` is called
- **THEN** it returns false with appropriate error messages

### Requirement: Spec artifact persistence

The system SHALL persist the following spec artifacts via `ISpecStore`:
- `.aos/spec/roadmap.json`: Main roadmap specification
- `.aos/spec/milestones/MS-0001/milestone.json`: Milestone spec
- `.aos/spec/phases/PH-0001/phase.json`: Foundation phase stub
- `.aos/spec/phases/PH-0002/phase.json`: Implementation phase stub
- `.aos/spec/phases/PH-0003/phase.json`: Validation phase stub
- `.aos/spec/milestones/index.json`: Updated milestone catalog
- `.aos/spec/phases/index.json`: Updated phase catalog

All artifacts MUST:
- Use UTF-8 encoding with LF line endings
- Validate against their respective schemas
- Include required schema identifiers (`nirmata:aos:schema:*:v1`)

#### Scenario: Roadmapper writes all spec artifacts

- **GIVEN** a successful roadmap generation
- **WHEN** the Roadmapper completes
- **THEN** all spec files exist and validate against their schemas

### Requirement: State management with cursor positioning

The system SHALL write `.aos/state/state.json` with:
- Cursor positioned at first phase (PH-0001)
- `cursor.phaseId = "PH-0001"`
- `cursor.phaseStatus = "pending"`
- Full cursor structure with null task/step fields

The state MUST validate against schema `nirmata:aos:schema:state:v1`.

#### Scenario: State file created with correct cursor

- **GIVEN** a successful roadmap generation
- **WHEN** the Roadmapper completes
- **THEN** `.aos/state/state.json` exists with cursor at PH-0001 with pending status

### Requirement: Event capture

The system SHALL append a `roadmap.created` event to `.aos/state/events.ndjson`.

The event MUST include:
- `eventType`: "roadmap.created"
- `timestampUtc`: ISO 8601 timestamp
- `runId`: Current run identifier
- `correlationId`: Correlation ID from context
- `data.roadmapId`: Generated roadmap identifier
- `data.milestoneCount`: Number of milestones created
- `data.phaseCount`: Number of phases created
- `data.firstPhaseId`: ID of the first phase (PH-0001)

The event MUST validate against schema `nirmata:aos:schema:event:v1`.

#### Scenario: Event written to events.ndjson

- **GIVEN** a successful roadmap generation with runId "run-123"
- **WHEN** the Roadmapper completes
- **THEN** `.aos/state/events.ndjson` contains a roadmap.created event with matching runId and roadmapId

### Requirement: Roadmapper integrates with orchestrator phase dispatch

The system SHALL provide a `RoadmapperHandler` implementing the phase handler contract used by the orchestrator.

The handler MUST:
- Accept commands for the Roadmapper phase
- Delegate to `IRoadmapper.GenerateRoadmapAsync`
- On success: return result indicating `HasRoadmap: true`
- On failure: return result with error details

#### Scenario: Handler executes roadmap generation

- **GIVEN** a gating result targeting the Roadmapper phase
- **WHEN** the handler executes
- **THEN** it generates the roadmap and writes all artifacts

#### Scenario: Handler updates gating context after success

- **GIVEN** a successful roadmap generation
- **WHEN** the handler completes
- **THEN** the returned result indicates `HasRoadmap: true` for subsequent gating

## Implementation Notes

### Default Structure

The Roadmapper generates a standard three-phase structure:

1. **Foundation (PH-0001)**: Project setup, infrastructure, CI/CD pipeline, core dependencies
2. **Implementation (PH-0002)**: Core features, integration components, documentation
3. **Validation (PH-0003)**: Test results, UAT sign-off, deployment artifacts

All phases map to the single milestone **Initial Delivery (MS-0001)**.

### File Locations

- `nirmata.Agents/Workflows/Planning/IRoadmapper.cs`
- `nirmata.Agents/Workflows/Planning/Roadmapper.cs`
- `nirmata.Agents/Workflows/Planning/IRoadmapGenerator.cs`
- `nirmata.Agents/Workflows/Planning/RoadmapGenerator.cs`
- `nirmata.Agents/Workflows/Planning/RoadmapperHandler.cs`
- `nirmata.Agents/Models/Runtime/RoadmapContext.cs`
- `nirmata.Agents/Models/Results/RoadmapResult.cs`
- `nirmata.Agents/Models/Contracts/MilestoneItem.cs`
- `nirmata.Agents/Models/Contracts/PhaseItem.cs`

### Testing

- Unit tests: `tests/nirmata.Agents.Tests/Workflows/Planning/RoadmapGeneratorTests.cs`
- Unit tests: `tests/nirmata.Agents.Tests/Workflows/Planning/RoadmapperTests.cs`
- Integration tests: `tests/nirmata.Agents.Tests/Workflows/Planning/RoadmapperIntegrationTests.cs`

All tests verify schema compliance through `AosWorkspaceValidator`.
