## ADDED Requirements

### Requirement: Workspace pages show the current gate as a first-class status surface
The system SHALL show a workspace-scoped gate status surface that exposes the current control-plane gate, the next required step, and the blocking reason from canonical workspace state.

#### Scenario: Open a workspace with an active blocking gate
- **WHEN** a user opens a workspace page that participates in the workflow surface
- **THEN** the page shows the current workspace gate
- **AND** the page shows the next required step needed to advance the workspace
- **AND** the page explains the artifact-backed reason the workspace is currently blocked

#### Scenario: Open different pages for the same workspace
- **WHEN** a user opens `WorkspaceDashboard`, `ChatPage`, and `CodebasePage` for the same workspace state
- **THEN** each page shows the same current gate and next required step
- **AND** page-specific presentation does not change the underlying workflow state being reported

### Requirement: Brownfield preflight and codebase readiness are explicit in the status surface
The system SHALL present brownfield preflight and codebase map readiness as explicit workflow status details whenever those states affect progression.

#### Scenario: Non-new workspace is blocked on missing codebase map
- **WHEN** the workspace has a persisted project specification
- **AND** canonical state indicates `.aos/codebase/map.json` is absent for a non-new workspace
- **THEN** the status surface shows that brownfield preflight is blocking roadmap or planning progression
- **AND** the status surface identifies codebase mapping as the next required step

#### Scenario: Non-new workspace is blocked on stale codebase map
- **WHEN** canonical state indicates the codebase map is stale for a non-new workspace
- **THEN** the status surface shows the workspace as blocked on refreshing codebase readiness
- **AND** the blocking detail makes it clear that the current map is stale rather than missing

### Requirement: Status surface provides a route to the blocking action
The system SHALL provide a primary route-to-action affordance for the current blocking gate.

#### Scenario: User activates the primary action for the current gate
- **WHEN** a user selects the primary action shown in the status surface
- **THEN** the system routes the user to the page or workflow entry point that can resolve the active gate
- **AND** the target reflects the same workspace-scoped gate context the status surface described
