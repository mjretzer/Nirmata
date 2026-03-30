# workspace-registry Specification

## Purpose
Define workspace registry persistence and CRUD semantics, including how workspace readiness/status is derived from git-backed roots and AOS scaffolding.
## Requirements
### Requirement: Workspace registry persistence
The system SHALL persist a registry of workspaces so that registered workspaces are available across API restarts.

#### Scenario: Registered workspaces are available after restart
- **WHEN** one or more workspaces are registered
- **AND** the API process is restarted
- **THEN** a subsequent request to list workspaces returns the previously registered workspaces

### Requirement: Register a workspace root path
The system SHALL allow a client to register a workspace root path only when the path is an absolute path to a valid git repository root and receive a stable workspace identifier.

#### Scenario: Register workspace
- **WHEN** a client registers a valid workspace root path that already contains a git repository
- **THEN** the system creates a workspace registry entry
- **AND** the system returns a stable workspace identifier

### Requirement: Deregister a workspace
The system SHALL allow a client to deregister a workspace by its workspace identifier.

#### Scenario: Deregister workspace
- **WHEN** a client deregisters an existing workspace by ID
- **THEN** the workspace is removed from the registry
- **AND** subsequent list requests do not include that workspace

### Requirement: Update a workspace root path
The system SHALL allow updating a workspace’s registered root path without changing the workspace identifier.

#### Scenario: Update workspace path
- **WHEN** a client updates a workspace’s root path
- **THEN** the registry stores the new path for that workspace
- **AND** the workspace identifier remains unchanged

### Requirement: Workspace status is derived from filesystem inspection
The system SHALL derive a workspace status from the workspace root on each read so the API can surface whether the workspace appears initialized from both `.git/` and `.aos/` presence.

#### Scenario: Workspace has `.aos/` and `.git/`
- **WHEN** a workspace root contains both a `.aos/` directory and a `.git/` directory
- **THEN** the workspace summary status indicates the workspace is initialized

#### Scenario: Workspace does not have `.git/`
- **WHEN** a workspace root does not contain a `.git/` directory
- **THEN** the workspace summary status indicates the workspace is not initialized

#### Scenario: Workspace does not have `.aos/`
- **WHEN** a workspace root does not contain a `.aos/` directory
- **THEN** the workspace summary status indicates the workspace is not initialized

### Requirement: Invalid or inaccessible workspace roots are surfaced as non-ready status
The system SHALL surface an explicit non-ready status when a registered workspace root does not exist or cannot be accessed.

#### Scenario: Workspace root path is missing
- **WHEN** a workspace root path does not exist on disk
- **THEN** listing workspaces returns the workspace with a non-ready status

