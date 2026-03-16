## ADDED Requirements

### Requirement: Workspace Picker Page

The `nirmata.Web` project SHALL provide a `/Workspace` page for selecting and managing AOS workspace directories.

The implementation MUST:
- Accept workspace path via text input with directory browse capability
- Display recent workspaces list with name, path, last opened, last run status
- Provide buttons: Open, Init (.aos), Validate workspace, Repair indexes
- Show workspace health summary: `.aos` present, schemas ok, locks present, last run
- Persist selection in configuration and display current config
- Prevent path traversal attacks by validating paths

#### Scenario: Display empty workspace picker
- **GIVEN** no workspace is currently configured
- **WHEN** a user navigates to `/Workspace`
- **THEN** the page displays a path input, browse button, and "Open" button
- **AND** a "Recent Workspaces" section shows "No recent workspaces"

#### Scenario: Open valid workspace
- **GIVEN** a valid `.aos/` directory exists at `C:\Projects\MyApp\.aos`
- **WHEN** a user enters the path and clicks "Open"
- **THEN** the workspace health summary shows: `.aos present: ✓`, `schemas ok: ✓`, `locks: none`, `last run: 2024-01-15`
- **AND** the workspace is added to recent list

#### Scenario: Initialize new workspace
- **GIVEN** a directory exists without `.aos/` subdirectory
- **WHEN** a user clicks "Init (.aos)"
- **THEN** the system creates `.aos/spec/`, `.aos/state/`, `.aos/evidence/`, `.aos/codebase/`, `.aos/context/`, `.aos/cache/` directories
- **AND** a default `spec/project.json` template is created

#### Scenario: Validate workspace with errors
- **GIVEN** a workspace with corrupted `state.json`
- **WHEN** a user clicks "Validate workspace"
- **THEN** the validation report shows specific schema errors
- **AND** guidance for repair is displayed

#### Scenario: Block path traversal attempt
- **GIVEN** a user enters path `../../../etc/passwd`
- **WHEN** submission is attempted
- **THEN** the system rejects the path with "Invalid workspace path" error
