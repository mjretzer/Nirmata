# Roadmapper Workflow

The Roadmapper workflow transforms a validated project specification into a structured execution roadmap with milestones, phases, and associated artifacts.

## Overview

The Roadmapper is a critical component of the GMSD orchestration pipeline that bridges the gap between project definition and executable planning phases. It generates deterministic milestone and phase structures based on the validated project specification.

## Components

### Interfaces

- **`IRoadmapper`** - Defines the contract for roadmap generation
- **`IRoadmapGenerator`** - Generates milestone and phase skeletons

### Models

- **`RoadmapContext`** - Execution context containing RunId, WorkspacePath, and ProjectSpec reference
- **`RoadmapResult`** - Result of roadmap generation including milestones, phases, and artifacts
- **`MilestoneItem`** - Skeleton model for milestone generation
- **`PhaseItem`** - Skeleton model for phase generation

### Implementations

- **`Roadmapper`** - Default implementation that generates specs, persists artifacts, and manages state
- **`RoadmapGenerator`** - Generates the default milestone (MS-0001: Initial Delivery) and phases (PH-0001-0003)
- **`RoadmapperHandler`** - Orchestrator integration handler

## Default Structure

The Roadmapper generates a default project structure:

### Milestone: MS-0001 (Initial Delivery)
- **PH-0001: Foundation** - Project setup, CI/CD, core dependencies
- **PH-0002: Implementation** - Core features and functionality
- **PH-0003: Validation** - Testing, UAT, deployment preparation

## Artifact Generation

The workflow creates the following artifacts:

```
.aos/
├── spec/
│   ├── roadmap.json              # Roadmap specification
│   ├── milestones/
│   │   ├── index.json            # Milestone catalog
│   │   └── MS-0001/
│   │       └── milestone.json    # Milestone spec
│   └── phases/
│       ├── index.json            # Phase catalog
│       ├── PH-0001/phase.json    # Foundation phase
│       ├── PH-0002/phase.json    # Implementation phase
│       └── PH-0003/phase.json    # Validation phase
└── state/
    ├── state.json                # Cursor positioned at PH-0001
    └── events.ndjson             # roadmap.created event
```

## Usage

```csharp
// Create context
var context = new RoadmapContext
{
    RunId = "run-123",
    WorkspacePath = "/path/to/workspace",
    ProjectSpec = new ProjectSpecReference { ... }
};

// Generate roadmap
var roadmapper = new Roadmapper(generator, specStore);
var result = await roadmapper.GenerateRoadmapAsync(context);

// Check result
if (result.IsSuccess)
{
    Console.WriteLine($"Generated roadmap: {result.RoadmapId}");
    Console.WriteLine($"Milestones: {result.MilestoneSpecs.Count}");
    Console.WriteLine($"Phases: {result.PhaseSpecs.Count}");
}
```

## Integration

The Roadmapper integrates with the orchestrator workflow:

1. **Gating**: The `GatingEngine` checks `HasRoadmap` context
2. **Dispatch**: `RoadmapperHandler` processes Roadmapper phase commands
3. **State**: Cursor is positioned at PH-0001 after successful generation
4. **Events**: `roadmap.created` event is appended to `events.ndjson`

## Testing

- **Unit Tests**: `RoadmapGeneratorTests.cs`, `RoadmapperTests.cs`
- **Integration Tests**: `RoadmapperIntegrationTests.cs` with temp workspace validation

## Schema Compliance

All generated artifacts validate against GMSD AOS schemas:
- `gmsd:aos:schema:roadmap:v1`
- `gmsd:aos:schema:milestone:v1`
- `gmsd:aos:schema:phase:v1`
- `gmsd:aos:schema:state:v1`
- `gmsd:aos:schema:event:v1`
