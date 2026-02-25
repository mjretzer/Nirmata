## MODIFIED Requirements
### Requirement: Artifact IDs route to exactly one canonical contract path
The system SHALL provide a single source of truth that maps each supported artifact ID to exactly one deterministic contract path under `.aos/*`.

Routing MUST be deterministic and MUST NOT depend on the current working directory, OS path separators, or filesystem scanning.

#### Scenario: Milestone ID routes to milestone contract path
- **GIVEN** a milestone ID `MS-0001`
- **WHEN** the system resolves its contract path
- **THEN** the contract path is `.aos/spec/milestones/MS-0001/milestone.json`

#### Scenario: Phase ID routes to phase contract path
- **GIVEN** a phase ID `PH-0001`
- **WHEN** the system resolves its contract path
- **THEN** the contract path is `.aos/spec/phases/PH-0001/phase.json`

#### Scenario: Task ID routes to task contract path
- **GIVEN** a task ID `TSK-000001`
- **WHEN** the system resolves its contract path
- **THEN** the contract path is `.aos/spec/tasks/TSK-000001/task.json`

#### Scenario: Issue ID routes to issue contract path
- **GIVEN** an issue ID `ISS-0001`
- **WHEN** the system resolves its contract path
- **THEN** the contract path is `.aos/spec/issues/ISS-0001.json`

#### Scenario: UAT ID routes to uat contract path
- **GIVEN** a uat ID `UAT-0001`
- **WHEN** the system resolves its contract path
- **THEN** the contract path is `.aos/spec/uat/UAT-0001.json`

#### Scenario: RUN ID routes to run evidence root contract path
- **GIVEN** a run ID in the current engine format
- **WHEN** the system resolves its run evidence root contract path
- **THEN** the contract path is `.aos/evidence/runs/<run-id>/`

