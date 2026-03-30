## Why

Workspace creation currently allows users to register or open a folder without guaranteeing that the folder is a valid git repository. That leaves the app in a half-initialized state, while the engine and downstream workflows assume git-backed workspace semantics. We need to make git repository setup part of workspace creation so users never land in an invalid workspace.

## What Changes

- Make workspace creation a bootstrap flow that creates or validates a git repository before the workspace is considered usable.
- Ensure the app can initialize the repository for the user instead of asking them to run `git init` manually.
- Update the workspace launcher, workspace dashboard, and settings pages to route users through the real bootstrap flow instead of demo-only git toggles and to surface failed initialization clearly.
- Align workspace registration and status reporting with the new bootstrap requirement so incomplete folders are not treated as ready workspaces.
- Add or update backend support so workspace creation can perform git initialization and AOS scaffold setup in one path.

## Capabilities

### New Capabilities
- `workspace-bootstrap`: Create a workspace root that includes a valid git repository and the required AOS bootstrap artifacts before the workspace can be used.

### Modified Capabilities
- `workspace-registry`: Workspace registration and workspace-ready status must reflect bootstrap readiness, including git repository presence, instead of allowing partially initialized folders to appear usable.

## Impact

- Frontend workspace creation and settings flows in `nirmata.frontend/src/app/pages/WorkspaceLauncherPage.tsx`, `WorkspaceDashboard.tsx`, and `SettingsPage.tsx`.
- Workspace bootstrap and validation hooks in `nirmata.frontend/src/app/hooks/useAosData.ts`.
- Backend workspace registration and service logic in `src/nirmata.Api/Controllers/V1/WorkspacesController.cs` and `src/nirmata.Services/Implementations/WorkspaceService.cs`.
- AOS workspace bootstrap behavior and any git initialization helper used during workspace setup.
- Tests covering workspace creation, initialization, and repo readiness.
