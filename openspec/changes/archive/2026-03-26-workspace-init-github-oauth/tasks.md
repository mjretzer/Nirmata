## 1. Backend GitHub OAuth and provisioning

- [x] 1.1 Add a GitHub OAuth start/callback flow that preserves workspace creation context and exchanges the authorization code securely.
- [x] 1.2 Add a GitHub repository provisioning service that creates or reuses the remote repo using the authorized account.
- [x] 1.3 Extend the existing workspace bootstrap pipeline so it can configure `origin` for the created repository without destroying existing history.
- [x] 1.4 Register the created workspace after the GitHub-connected bootstrap succeeds and surface clear failure states on the callback path.

## 2. Frontend first-init integration

- [x] 2.1 Update `WorkspaceLauncherPage` so first-init can launch the GitHub-connected flow instead of only local bootstrap.
- [x] 2.2 Show the connected GitHub/account/repo state and success or failure feedback on the first-init screen.
- [x] 2.3 Keep the local-only bootstrap path available as a fallback for users who do not want GitHub.

## 3. Configuration and diagnostics

- [x] 3.1 Add GitHub OAuth configuration for client ID, client secret, and callback URL.
- [x] 3.2 Update API/client diagnostics so OAuth and repo-creation failures show actionable messages.
- [x] 3.3 Add any settings-page affordance needed to retry or reconnect GitHub for an existing workspace.

## 4. Verification

- [x] 4.1 Add backend tests for OAuth state validation, repo creation success, repo reuse, and failure handling.
- [x] 4.2 Add frontend tests for the GitHub-connected init flow, success/error states, and fallback behavior.
- [x] 4.3 Verify the first-init and settings copy clearly states that GitHub connection can create the repo and set origin automatically.
