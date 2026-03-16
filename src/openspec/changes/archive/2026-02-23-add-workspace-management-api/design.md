# Design: add-workspace-management-api

## Architecture
The Workspace Management system follows a layered architecture:
1. **API Layer (`WorkspaceController`)**: Exposes REST endpoints for the UI.
2. **Service Layer (`WorkspaceService`)**: Orchestrates metadata persistence and physical engine calls.
3. **Engine Layer (`AosWorkspaceBootstrapper`)**: Performs idempotent file system operations and validation.
4. **Data Layer (`WorkspaceRepository`)**: Manages the local SQLite database for workspace metadata (last opened, path, health).

## Key Decisions

### 1. Configuration Persistence
**Current**: Uses `%LOCALAPPDATA%\nirmata\workspace-config.json` for tracking the "active" workspace and shared preferences.
**Proposed**: Move to a dual-mode system:
- Global "recent workspaces" index remains in `%LOCALAPPDATA%` (managed by `WorkspaceRepository`).
- Workspace-specific settings (e.g., agent preferences, engine overrides) move to `.aos/config.json`.
- **Migration**: On first open, the system SHALL check for global config and migrate relevant workspace-specific keys to the new `.aos/config.json`.

### 2. Repair Logic
The "Repair" operation will be enhanced to:
- Re-run `EnsureInitialized` to seed missing files.
- Re-scan `spec/` directories and rebuild `index.json` files.
- Validate all JSON artifacts against their registered schemas.
- **Concurrency**: Repair operations MUST acquire a workspace-level lock to prevent concurrent modification by the engine.

### 3. Validation Reporting
The `WorkspaceValidationReport` will be extended to include:
- `SchemaVersion` checks against the `AosSchemaRegistry`.
- Lock file status and ownership.
- Broken cross-references between artifacts (e.g., a task referencing a non-existent agent).

## Data Model
### Workspace Metadata (SQLite)
- `Id`: GUID
- `Path`: String (Absolute)
- `Name`: String
- `LastOpened`: DateTime
- `LastValidated`: DateTime
- `HealthStatus`: Enum (Healthy, Warning, Error)

### Workspace Config (.aos/config.json)
- `Version`: Integer
- `AgentPreferences`: Object
- `EngineOverrides`: Object
- `ExcludedPaths`: List<String>
