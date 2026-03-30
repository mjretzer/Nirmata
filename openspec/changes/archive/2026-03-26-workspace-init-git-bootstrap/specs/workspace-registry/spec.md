# workspace-registry Specification

## Purpose
TBD - created by archiving change workspace-registry-domain-data-foundation. Update Purpose after archive.

## MODIFIED Requirements

### Requirement: Register a workspace root path
The system SHALL allow a client to register a workspace root path only when the path is an absolute path to a valid git repository root and receive a stable workspace identifier.

#### Scenario: Register workspace
- **WHEN** a client registers a valid workspace root path that already contains a git repository
- **THEN** the system creates a workspace registry entry
- **AND** the system returns a stable workspace identifier

### Requirement: Workspace status is derived from filesystem inspection
The system SHALL derive a workspace status from the workspace root on each read so the API can surface whether the workspace appears initialized.

#### Scenario: Workspace has `.aos/` and `.git/`
- **WHEN** a workspace root contains both a `.aos/` directory and a `.git/` directory
- **THEN** the workspace summary status indicates the workspace is initialized

#### Scenario: Workspace does not have `.git/`
- **WHEN** a workspace root does not contain a `.git/` directory
- **THEN** the workspace summary status indicates the workspace is not initialized

#### Scenario: Workspace does not have `.aos/`
- **WHEN** a workspace root does not contain a `.aos/` directory
- **THEN** the workspace summary status indicates the workspace is not initialized
