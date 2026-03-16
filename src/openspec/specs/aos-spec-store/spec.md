# aos-spec-store Specification

## Purpose

Defines canonical AOS workspace contracts and behavioral semantics for $capabilityId.

- **Lives in:** `nirmata.Aos/*`, `.aos/**`
- **Owns:** Engine-level artifact contracts, validation, and deterministic IO semantics for this capability
- **Does not own:** Plane/orchestrator workflows (owned by `agents-*` capabilities)
## Requirements
### Requirement: Spec store resolves all artifact paths via routing
All functionality that reads or writes spec-layer artifacts under `.aos/spec/**` MUST resolve paths via the routing source of truth defined by `aos-path-routing`.

#### Scenario: Store resolves a milestone path via routing
- **GIVEN** a milestone ID `MS-0001`
- **WHEN** the spec store resolves the artifact path
- **THEN** the resolved contract path is `.aos/spec/milestones/MS-0001/milestone.json`

### Requirement: Spec store supports CRUD for spec artifacts
The system SHALL provide a spec store that can create, read, update, and delete the following spec artifacts using their canonical contract paths:
- `.aos/spec/project.json`
- `.aos/spec/roadmap.json`
- `.aos/spec/milestones/MS-####/milestone.json`
- `.aos/spec/phases/PH-####/phase.json`
- `.aos/spec/tasks/TSK-######/task.json`
- `.aos/spec/tasks/TSK-######/plan.json`
- `.aos/spec/tasks/TSK-######/links.json`
- `.aos/spec/issues/ISS-####.json`
- `.aos/spec/uat/UAT-####.json`

All JSON artifacts written by the spec store MUST be written using the canonical deterministic JSON writer defined by `aos-deterministic-json-serialization`.

#### Scenario: Creating a task writes deterministic JSON to the canonical path
- **GIVEN** a valid task document for `TSK-000001`
- **WHEN** the spec store creates the task
- **THEN** `.aos/spec/tasks/TSK-000001/task.json` exists and is deterministic JSON

#### Scenario: Creating an issue writes deterministic JSON to the flat canonical path
- **GIVEN** a valid issue document for `ISS-0001`
- **WHEN** the spec store creates the issue
- **THEN** `.aos/spec/issues/ISS-0001.json` exists and is deterministic JSON

### Requirement: Spec store maintains catalog indexes deterministically
When the spec store creates or deletes a milestone/phase/task/issue/uat artifact, it SHALL update the corresponding catalog index file deterministically:
- `.aos/spec/milestones/index.json`
- `.aos/spec/phases/index.json`
- `.aos/spec/tasks/index.json`
- `.aos/spec/issues/index.json`
- `.aos/spec/uat/index.json`

Catalog indexes MUST conform to the catalog index contract:
- `schemaVersion` = 1
- `items` = array of artifact IDs (strings)

The `items` array MUST be sorted using **ordinal** string ordering.

#### Scenario: Creating an issue updates the issue catalog index deterministically
- **GIVEN** a workspace where `.aos/spec/issues/index.json` exists
- **WHEN** the spec store creates `ISS-0001`
- **THEN** `.aos/spec/issues/index.json` includes `ISS-0001` and remains deterministically sorted

