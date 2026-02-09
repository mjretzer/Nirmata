# aos-path-routing Specification

## Purpose
TBD - created by archiving change add-paths-and-id-routing. Update Purpose after archive.
## Requirements
### Requirement: Artifact IDs are parsed deterministically
The system SHALL parse and validate AOS artifact IDs deterministically so they can be used for routing without ambiguity.

Supported IDs MUST have the following formats:
- `MS-####` (4 digits; milestone)
- `PH-####` (4 digits; phase)
- `TSK-######` (6 digits; task)
- `ISS-####` (4 digits; issue)
- `UAT-####` (4 digits; uat)
- `PCK-####` (4 digits; context pack)
- `RUN` IDs MUST remain the engine’s current format: 32 lower-case hex characters (GUID `N` format).

#### Scenario: Supported spec IDs are accepted
- **WHEN** the user supplies each of `MS-0001`, `PH-0001`, `TSK-000001`, `ISS-0001`, `UAT-0001`, and `PCK-0001`
- **THEN** the IDs are accepted as valid and can be routed

#### Scenario: Malformed IDs are rejected
- **WHEN** the user supplies an ID with the wrong prefix or wrong digit count (e.g., `MS-1`, `PH-00001`, `TSK-001`, `UAT-00001`, `PCK-01`)
- **THEN** validation fails with an actionable error describing the expected format

#### Scenario: RUN IDs are validated using the current engine format
- **WHEN** the user supplies a RUN ID that is not 32 lower-case hex characters
- **THEN** validation fails with an actionable error describing the expected format

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

#### Scenario: Context pack ID routes to context pack contract path
- **GIVEN** a context pack ID `PCK-0001`
- **WHEN** the system resolves its contract path
- **THEN** the contract path is `.aos/context/packs/PCK-0001.json`

#### Scenario: RUN ID routes to run evidence root contract path
- **GIVEN** a run ID in the current engine format
- **WHEN** the system resolves its run evidence root contract path
- **THEN** the contract path is `.aos/evidence/runs/<run-id>/`

### Requirement: Routing is the only allowed mechanism for path resolution
All engine and CLI functionality that reads/writes AOS artifacts under `.aos/*` MUST obtain the artifact paths via the routing source of truth.

#### Scenario: Call sites do not use ad-hoc `.aos/*` path building
- **WHEN** a developer adds a new command that reads or writes a milestone, phase, task, issue, uat, or run artifact
- **THEN** the command uses the routing source of truth rather than building `.aos/*` paths ad-hoc

### Requirement: Canonical non-ID contract paths are centralized
The system SHALL centralize canonical non-ID contract paths under `.aos/*` alongside ID-based routing so callers do not build paths ad-hoc.

At minimum, the routing source of truth MUST define the workspace lock contract path:
- `.aos/locks/workspace.lock.json`

#### Scenario: Workspace lock contract path is canonical
- **WHEN** the system needs the workspace lock file
- **THEN** it uses the centralized contract path `.aos/locks/workspace.lock.json`

### Requirement: Contract paths are platform-neutral and safe
Canonical contract paths MUST:
- use forward slashes (`/`) as separators (platform-neutral)
- start with `.aos/`
- not contain `.` or `..` path segments

#### Scenario: Contract paths are rejected when they contain backslashes
- **WHEN** a contract path containing `\\` is provided to the path resolver
- **THEN** the contract path is rejected with an actionable error

#### Scenario: Contract paths are rejected when they contain dot segments
- **WHEN** a contract path contains `.` or `..` segments
- **THEN** the contract path is rejected with an actionable error

