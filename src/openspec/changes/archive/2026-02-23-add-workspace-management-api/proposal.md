# Proposal: add-workspace-management-api

## Goal
Implement the backend services and API for workspace management as defined in PH-PRD-0012. This includes workspace listing, initialization, validation, and repair, as well as aligning workspace configuration persistence.

## Why
Currently, workspace management is fragmented across `%LOCALAPPDATA%` and the local file system without a unified API or consistent validation. This creates friction when onboarding new workspaces, repairing corrupted indexes, or managing workspace-specific preferences.

## What Changes
- **Unified Workspace API**: Centralized management of workspace lifecycle (Init, Validate, Repair, List).
- **Configuration Alignment**: **BREAKING** Migration of workspace settings from global storage to `.aos/config.json`.
- **Deep Health Checks**: Implementation of schema-aware validation for all JSON artifacts.
- **Deterministic Repair**: Capability to rebuild `index.json` files and restore `.aos/` structure without data loss.

## Scope
- **nirmata.Web**: `WorkspaceService`, `WorkspaceController`, and `WorkspaceModels`.
- **nirmata.Aos**: `AosWorkspaceBootstrapper` enhancements for repair and index rebuilding.
- **nirmata.Data**: Workspace metadata persistence.
- **Persistence**: Migration from global `%LOCALAPPDATA%` to `.aos/config.json`.

## Capabilities
- **Workspace Repository**: CRUD for workspace metadata.
- **Initialization Service**: Bootstrap `.aos/` structure.
- **Validation Service**: Deep compliance checking (schema validity, locks).
- **Repair Service**: Index rebuilding and schema drift correction.
- **Config Integration**: Unified preference storage in `.aos/config.json`.

## Impact
- **Affected Specs**: `workspace-api-repair`, `workspace-api-validation`, `workspace-config-alignment`.
- **Affected Code**: `AosWorkspaceBootstrapper` (nirmata.Aos), `WorkspaceService` (nirmata.Web), `WorkspaceRepository` (nirmata.Data).
- **Breaking Changes**: Moving settings to `.aos/config.json` requires a migration path for existing users.

## Dependencies
- `web-workspace-api` spec.
- `aos-schema-registry` for validation.
