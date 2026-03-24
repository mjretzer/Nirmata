## Context

The current init flow has two path concepts that are too easy to confuse: the workspace root the user types in the UI, and the daemon's own current working directory or host profile workspace path. The bootstrapper already creates `.aos/` correctly once it is given the right repository root, so the fix should focus on making the selected root path authoritative for init rather than changing the bootstrap logic itself.

## Goals / Non-Goals

**Goals:**
- Make `aos init` create `.aos/` in the exact folder selected in the UI.
- Keep the init flow idempotent and non-destructive.
- Preserve the existing workspace bootstrapper behavior as the authoritative initializer.
- Add tests that verify explicit root-path execution, not just local UI state.

**Non-Goals:**
- Redesign the broader daemon command system.
- Change the `.aos/` folder layout.
- Remove host-profile based execution for unrelated command flows.

## Decisions

- **Pass an explicit root path for init**
  - Rationale: the user's typed folder should control where `.aos/` is created.
  - Alternative considered: continue relying on daemon host profile state. That keeps init fragile and detached from the UI.

- **Use the existing command path where possible**
  - Rationale: the daemon command pipeline already exists and should remain the execution path for `aos init`.
  - Alternative considered: bypass the daemon and call the bootstrapper directly from the frontend. That would duplicate behavior and bypass the service boundary.

- **Keep the bootstrapper authoritative**
  - Rationale: `AosWorkspaceBootstrapper.EnsureInitialized(...)` already owns directory creation, baseline file seeding, and idempotency.
  - Alternative considered: move `.aos/` creation into the controller or UI. That would scatter workspace bootstrap rules.

- **Preserve host-profile fallback for compatibility**
  - Rationale: other command flows may still depend on daemon runtime state, so the explicit init path should be additive rather than disruptive.
  - Alternative considered: require every command path to use host-profile state. That would not fix the init mismatch and would make setup harder to reason about.

## Risks / Trade-offs

- **API contract change risk** -> Adding an explicit working-directory field needs coordinated frontend/backend updates.
- **Path validation risk** -> The explicit path should be validated before dispatch so init fails fast on invalid or relative paths.
- **Behavior drift risk** -> The UI and daemon could diverge again if other flows keep inferring paths differently; tests should pin the init contract down.

## Migration Plan

1. Extend the command request contract so init can carry an explicit root path or working directory.
2. Update the frontend init hook to send the selected path instead of only `aos init`.
3. Update the daemon command controller to prefer the explicit path when running init.
4. Add tests for exact-path initialization, spaces in paths, and idempotent re-runs.
5. Verify the UI-created workspace path and daemon-run path now match.
