# aos-context-packs Specification

## Purpose
TBD - created by archiving change add-context-pack-builder. Update Purpose after archive.
## Requirements
### Requirement: Context packs are written as deterministic, self-contained AOS artifacts
The system SHALL support **context packs** as deterministic, self-contained JSON artifacts written under `.aos/context/packs/**`.

A context pack MUST:
- be written to a deterministic contract path rooted at `.aos/context/packs/`
- be valid JSON
- be written using the canonical deterministic JSON writer for AOS-emitted artifacts
- embed the selected artifact contents (not only references)

#### Scenario: A task-mode pack is created as a self-contained JSON artifact
- **GIVEN** a workspace with a task plan at `.aos/spec/tasks/TSK-000001/plan.json`
- **WHEN** a task-mode pack is built for `TSK-000001`
- **THEN** a pack JSON file exists under `.aos/context/packs/**`
- **AND** the pack contains embedded contents for the selected artifacts

### Requirement: Pack build is budgeted and deterministic
When building a context pack, the system MUST apply a deterministic budget policy so pack output is bounded and reproducible.

At minimum, budget enforcement MUST:
- define a deterministic selection order for candidate artifacts (ordinal order over contract paths)
- include artifacts in-order until the budget is reached
- exclude (not partially include) any artifact that would exceed the budget

#### Scenario: Pack build stops deterministically at the budget boundary
- **GIVEN** a fixed set of candidate artifacts for a pack build and a fixed byte budget
- **WHEN** the pack is built twice from the same workspace state
- **THEN** the resulting pack bytes are identical across both builds

### Requirement: Pack contents are restricted by mode
The system SHALL define allowed artifact sets per pack mode, and packs MUST NOT include artifacts outside the allowed set.

At minimum:
- **Task-mode** packs are driven by `.aos/spec/tasks/<TSK-######>/plan.json` and MUST include the driving task plan.
- **Phase-mode** packs are driven by `.aos/spec/phases/<PH-####>/phase.json` and MUST include the driving phase spec.

#### Scenario: Task-mode pack includes only allowed task-scoped artifacts
- **GIVEN** a task-mode pack build request for `TSK-000001`
- **WHEN** the pack is built
- **THEN** every embedded entry corresponds to an allowed contract path for task-mode

### Requirement: Packs validate against the local schema pack
The system SHALL validate a context pack against the local schema pack using schema id `gmsd:aos:schema:context-pack:v1`.

#### Scenario: Pack validates against context-pack schema
- **GIVEN** a built context pack at `.aos/context/packs/PCK-0001.json`
- **WHEN** the pack is validated using the local schema pack
- **THEN** validation succeeds with no schema issues

