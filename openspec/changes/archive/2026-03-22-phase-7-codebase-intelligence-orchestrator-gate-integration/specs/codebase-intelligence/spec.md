# codebase-intelligence Specification

## Purpose
Define workspace-scoped codebase intelligence derived from `.aos/codebase/` artifacts.

## ADDED Requirements
### Requirement: Workspace codebase artifact inventory
The system SHALL return a complete inventory of recognized codebase artifacts for a workspace.

#### Scenario: List workspace artifacts
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/codebase`
- **THEN** the system returns the workspace's codebase artifact records
- **AND** each record includes a stable artifact id, type, status, path, and last updated metadata

#### Scenario: Unknown workspace
- **WHEN** a client requests the codebase inventory for an unknown workspace
- **THEN** the system returns `404 Not Found`

### Requirement: Codebase artifact detail
The system SHALL return the payload for a single codebase artifact when requested by artifact id.

#### Scenario: Get artifact detail
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/codebase/{artifactId}` for a known artifact
- **THEN** the system returns that artifact's parsed payload

#### Scenario: Unknown artifact id
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/codebase/{artifactId}` for an unsupported artifact id
- **THEN** the system returns `404 Not Found`

### Requirement: Language and stack intelligence
The system SHALL surface language breakdown and stack metadata from `map.json` and `stack.json`.

#### Scenario: Read language and stack data
- **WHEN** a client requests the codebase inventory for a workspace with valid codebase artifacts
- **THEN** the system returns language breakdown data and stack entries derived from the workspace's codebase pack

### Requirement: Codebase artifact freshness
The system SHALL classify codebase artifacts as `ready`, `stale`, `missing`, or `error` using artifact presence and manifest validation.

#### Scenario: Artifact is present and matches manifest
- **WHEN** the workspace contains the artifact file and its hash matches the manifest
- **THEN** the artifact status is `ready`

#### Scenario: Artifact file is missing
- **WHEN** the workspace is missing a recognized artifact file
- **THEN** the artifact status is `missing`

#### Scenario: Manifest validation fails
- **WHEN** the artifact file exists but its contents do not match the stored manifest hash
- **THEN** the artifact status is `stale`
