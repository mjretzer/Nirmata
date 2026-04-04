## ADDED Requirements

### Requirement: Plan root route shows roadmap lens from canonical workspace data
The system SHALL render the Plan root lens for `.aos/spec` using canonical milestone, phase, and task summaries retrieved for the selected workspace.

#### Scenario: Open plan root
- **WHEN** a user opens `/ws/{workspaceId}/files/.aos/spec` with a valid workspace
- **THEN** the system renders the roadmap lens
- **AND** milestone, phase, and task counts are derived from canonical workspace data sources

### Requirement: Phase directory routes resolve to phase task lens
The system SHALL resolve phase directory paths under `.aos/spec/phases/{phaseId}` to a dedicated phase task lens for that phase.

#### Scenario: Open a known phase directory
- **WHEN** a user opens `/ws/{workspaceId}/files/.aos/spec/phases/PH-0001`
- **THEN** the system renders the phase task lens for `PH-0001`
- **AND** tasks shown in the lens are scoped to that phase from canonical task data

#### Scenario: Route precedence for phase directory vs file artifact
- **WHEN** a user opens `/ws/{workspaceId}/files/.aos/spec/phases/PH-0001/phase.json`
- **THEN** the system renders the artifact file viewer for `phase.json`
- **AND** the system does not render the phase directory lens for that path

#### Scenario: Open an unknown phase directory
- **WHEN** a user opens `/ws/{workspaceId}/files/.aos/spec/phases/PH-9999`
- **THEN** the system renders a missing-artifact state
- **AND** the state identifies the requested phase path

### Requirement: Artifact file routes use workspace-backed file/spec content
The system SHALL render `.aos/spec` artifact files using workspace-backed file or spec content and MUST NOT fabricate substitute artifact payloads.

#### Scenario: Open existing task plan artifact
- **WHEN** a user opens `/ws/{workspaceId}/files/.aos/spec/tasks/TSK-000001/plan.json` and the artifact exists
- **THEN** the system renders the artifact viewer with workspace-backed content

#### Scenario: Open missing artifact file
- **WHEN** a user opens a `.aos/spec` artifact path that does not exist for the workspace
- **THEN** the system renders a missing-artifact state instead of generated synthetic JSON
- **AND** the missing-artifact state includes the requested path

### Requirement: Plan lens state handling is explicit and deterministic
The system SHALL provide explicit loading, empty, and error states for plan lenses and artifact viewers.

#### Scenario: Data is still loading
- **WHEN** plan lens data or artifact content is still loading
- **THEN** the system renders a loading state
- **AND** does not display stale artifact content from a previous route

#### Scenario: Workspace data request fails
- **WHEN** the workspace data request for a plan lens fails
- **THEN** the system renders an error state with path context for the failed lens
