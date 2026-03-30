## Context

Workspace creation currently allows a path to be registered before the path is a usable git repository. The frontend launcher and settings pages already route users through an init flow, but the current behavior is mostly split across demo UI state, workspace registration, and AOS-only initialization. The engine and downstream workspace logic assume git-backed repositories, so the bootstrap path needs to create git state before a workspace is treated as usable.

## Goals / Non-Goals

**Goals:**
- Make git repository setup a required part of workspace creation.
- Keep bootstrap idempotent so existing repositories can be reused safely.
- Update launcher, dashboard, and settings flows so they guide users through real bootstrap instead of demo-only actions.
- Keep workspace registry state aligned with repo readiness so incomplete folders are not treated as ready workspaces.
- Preserve clear diagnostics when bootstrap fails.

**Non-Goals:**
- Remote repository hosting, authentication, or push/pull policy changes.
- Git branching UX beyond what is needed to bootstrap a local repository.
- Reworking workspace-scoped spec/state/file APIs.
- Introducing a new persistence model for workspaces.

## Decisions

1. **Centralize bootstrap in the existing init path**
   - Use the current workspace bootstrap flow as the authoritative place to initialize git and AOS artifacts.
   - This avoids duplicating git logic in the frontend and keeps filesystem mutation in one place.
   - **Alternatives considered:**
     - Frontend runs `git init` directly. Rejected because it duplicates platform-sensitive logic and spreads shelling out across UI code.
     - A separate git-only API. Rejected because it adds another user-facing path and increases failure modes.

2. **Register workspaces only after bootstrap succeeds**
   - A workspace should not enter the registry as usable until the root path has been bootstrapped successfully.
   - This preserves the requirement that a user cannot create a workspace without a valid git repository.
   - **Alternatives considered:**
     - Persist a partial registry entry and repair later. Rejected because it violates the user requirement and complicates readiness semantics.

3. **Treat git readiness as part of workspace readiness**
   - Workspace status should require both the git repository and AOS scaffold to exist before the workspace is considered initialized.
   - **Alternatives considered:**
     - Treat `.aos/` alone as initialized. Rejected because it would allow non-git folders to appear ready.

4. **Keep bootstrap idempotent**
   - Re-running bootstrap on an existing repository should preserve git history and only seed missing workspace artifacts.
   - **Alternatives considered:**
     - Always fail if git already exists. Rejected because existing repositories are a valid starting point.
     - Always reinitialize the repository. Rejected because it risks damaging user history.

5. **Replace demo repo controls with real bootstrap actions**
   - The dashboard and settings pages should expose real bootstrap actions and status, not local-only toggles.
   - The launcher should remain the primary creation entrypoint, while settings should be the repair/re-run path for existing workspaces.

## Risks / Trade-offs

- **[Partial filesystem state]** → Bootstrap can fail after creating some files or directories. Mitigation: keep operations ordered, make bootstrap idempotent, and surface repair guidance in settings.
- **[Host git availability]** → Git may be missing or misconfigured on the user machine. Mitigation: explicit diagnostics and clear failure messages before the workspace is marked usable.
- **[Existing workspaces become not-ready]** → Workspaces without `.git/` will now be treated as incomplete. Mitigation: provide a bootstrap/repair action in settings and launcher flows.
- **[Windows path quoting]** → Paths with spaces can break command execution. Mitigation: reuse the existing process execution patterns and working-directory handling already used by the daemon/engine.

## Migration Plan

1. Add backend/daemon support for bootstrapping git and AOS together.
2. Update frontend creation flows to call bootstrap before registering or opening a workspace.
3. Remove demo-only git toggles and replace them with real bootstrap/status actions.
4. Update workspace readiness checks so `.git/` is required alongside `.aos/`.
5. Surface repair guidance for any existing workspaces that are missing git state.

## Open Questions

- Should bootstrap create only `git init`, or also a default `.gitignore` and initial commit?
- Should the launcher create a new folder itself, or only bootstrap an existing selected root?
- Should existing non-git workspaces be auto-repaired on open, or only repaired when the user explicitly chooses to bootstrap them?
