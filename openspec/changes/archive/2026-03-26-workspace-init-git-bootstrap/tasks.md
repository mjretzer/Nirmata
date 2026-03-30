## 1. Bootstrap backend git support

- [x] 1.1 Extend the workspace bootstrap path so `aos init` creates or validates a git repository before seeding AOS artifacts.
- [x] 1.2 Keep bootstrap idempotent so re-running setup preserves existing git history and only fills missing workspace artifacts.
- [x] 1.3 Surface structured bootstrap failures for missing git, command execution errors, and partial initialization states.
- [x] 1.4 Update workspace registration/update validation so only git-backed roots can be treated as ready workspaces.

## 2. Update workspace creation and settings flows

- [x] 2.1 Update `WorkspaceLauncherPage` so folder initialization performs real bootstrap before a workspace is registered or opened.
- [x] 2.2 Replace the dashboard "Initialize New Workspace" demo behavior with the real bootstrap flow and success/error handling.
- [x] 2.3 Update `SettingsPage` root-path save so it requires successful bootstrap before marking the workspace setup complete.
- [x] 2.4 Remove or repurpose manual `git init` prompts and toggles so they reflect actual bootstrap state instead of local demo state.

## 3. Align workspace readiness and diagnostics

- [x] 3.1 Update workspace status handling so missing `.git/` is reported as not initialized or not ready.
- [x] 3.2 Ensure workspace bootstrap and registry copy/messages clearly explain that git is required for a usable workspace.
- [x] 3.3 Verify launcher, dashboard, and settings copy matches the new git-required workflow.

## 4. Test the end-to-end bootstrap flow

- [x] 4.1 Add backend tests for git bootstrap success, idempotency, and failure cases.
- [x] 4.2 Add frontend tests for launcher and settings gating, including bootstrap success and failure paths.
- [x] 4.3 Verify workspace list/status rendering reflects the new git-required readiness rules.
