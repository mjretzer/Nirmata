## MODIFIED Requirements
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

