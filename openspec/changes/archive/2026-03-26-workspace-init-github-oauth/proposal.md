## Why

First-time workspace setup still expects users to have a GitHub repository and `origin` ready ahead of time. That creates avoidable friction during init and leaves new workspaces incomplete unless users manually create a repo, connect it locally, and configure the remote.

## What Changes

- Add a GitHub-connected first-init flow that launches OAuth from the workspace creation screen.
- Create or reuse a GitHub repository automatically after authorization and set it as the local workspace `origin`.
- Keep the existing local bootstrap path available, but make the GitHub-connected path the recommended first-init experience.
- Surface clear success and failure states for OAuth approval, repo creation, and remote setup.
- Add backend support for exchanging the OAuth callback, creating the repo, and wiring the local git remote during workspace initialization.

## Capabilities

### New Capabilities
- `github-workspace-bootstrap`: Connect a GitHub account during workspace creation, create or reuse a GitHub repository, and set the local workspace `origin` automatically.

### Modified Capabilities

## Impact

- Frontend workspace creation UX in `nirmata.frontend/src/app/pages/WorkspaceLauncherPage.tsx`.
- Optional workspace setup affordances in `nirmata.frontend/src/app/pages/SettingsPage.tsx`.
- Frontend API wiring in `nirmata.frontend/src/app/hooks/useAosData.ts` and `nirmata.frontend/src/app/utils/apiClient.ts`.
- Backend workspace/bootstrap services in `src/nirmata.Services/Implementations/WorkspaceBootstrapService.cs` and new GitHub provisioning service/controller code.
- Configuration for GitHub OAuth client ID/secret and callback routing.
- Tests covering OAuth initiation, repo creation, remote setup, and failure handling.
