# Tasks: add-workspace-management-api

## Phase 1: Engine Enhancements (`nirmata.Aos`)
- [x] Implement index rebuilding logic in `AosWorkspaceBootstrapper` with directory scanning.
- [x] Add schema-aware validation to `CheckCompliance` using `AosSchemaRegistry`.
- [x] Implement `.aos/config.json` model and read/write support in `AosWorkspaceBootstrapper`.
- [x] Add workspace-level locking for `Repair` operations.

## Phase 2: Metadata & Repository (`nirmata.Data`)
- [x] Update `WorkspaceEntity` to include `LastValidated` and `HealthStatus`.
- [x] Update `WorkspaceRepository` to support health history and detailed metadata.
- [x] Create and run migration for database schema updates.

## Phase 3: Web Services & API (`nirmata.Web`)
- [x] Implement `WorkspaceService.RepairAsync` and `WorkspaceService.ValidateAsync`.
- [x] Implement transition/migration logic for configuration from `%LOCALAPPDATA%` to `.aos/config.json`.
- [x] Implement `WorkspaceController` with `POST /repair`, `GET /validate`, and `POST /init` endpoints.
- [x] Add structured error mapping for workspace operations in `WorkspaceController`.

## Phase 4: Observability & Logging
- [x] Add detailed logging for repair and validation operations.
- [x] Add metrics for workspace health status across all registered workspaces.

## Phase 5: Verification
- [x] Unit tests for `AosWorkspaceBootstrapper` repair and index rebuilding logic.
- [x] Unit tests for `AosWorkspaceBootstrapper` config migration logic.
- [x] Integration tests for `WorkspaceService` and `WorkspaceRepository`.
- [x] E2E API tests for all new `WorkspaceController` endpoints.
- [x] `openspec validate add-workspace-management-api --strict`.
