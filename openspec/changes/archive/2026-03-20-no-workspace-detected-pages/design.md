# Design: No-workspace detected pages

## Key observation (current patterns)
- The app has a single Layout with sidebar navigation, top ribbon, optional file explorer, and an outlet.
- Most feature pages are mounted under `ws/:workspaceId` routes.
- The launcher page (`/`) is the only route that is clearly "no workspace".

The missing state is **"global navigation"**: a way to reach major consoles when the user is not yet scoped to a workspace, and have them render a consistent "No workspace detected" treatment.

Another missing state is **"empty folder"**: when the user selects a valid but empty directory, the app should offer to initialize a workspace in that folder rather than stating "Workspace not found".

## Proposed approach

### 1) Determine whether the app is workspace-scoped
Treat the app as workspace-scoped only when the current pathname matches:

- `/ws/:workspaceId/...`

Otherwise, the app is in "no workspace" mode.

### 2) Add explicit no-workspace routes (one per main nav page)
Add top-level routes that mirror the main navigation surface but do not require a workspace id:

- `/chat`
- `/plan`
- `/verification`
- `/runs`
- `/continuity`
- `/codebase`
- `/settings`
- `/host` (optional, but useful for direct entry)
- `/diagnostics` (optional)

Each of these routes renders a dedicated page component that displays a standardized "No workspace detected" state.

Important: these unscoped routes must render **only** the blank-state UI (do not mount the workspace-scoped console pages behind them).

Rationale:
- Avoids pushing fake ids into `/ws/:workspaceId/...`.
- Keeps workspace-scoped pages clean and focused.
- Makes navigation deterministic from the launcher.

### 3) Standardize the empty state UI
Create a shared component for the empty state, e.g. `NoWorkspaceDetected` with:

- Title: "No workspace detected"
- Short, page-specific description (what this page needs a workspace for)
- Primary CTA: "Go to Workspace Launcher" → navigates to `/`
- Optional secondary CTA: "Why do I need a workspace?" (collapsible help text)

Keep styling consistent with existing patterns:
- Centered card-like block, icon-in-circle, short text, one primary action.

### 4) Update navigation to use the no-workspace routes when unscoped
Update navigation generation (and any other workspace-id derivations like breadcrumbs) to:

- Prefer workspace-scoped links when currently under `/ws/:workspaceId/...`
- Otherwise link to the new no-workspace routes (e.g. `/plan` instead of `/ws/my-app/files/.aos/spec`)

This ensures "no workspace" mode does not fabricate an id.

Entry points that must be updated:
- Sidebar navigation in `Layout`
- `TopRibbon` workspace-id derivation, breadcrumb root, and any embedded command-palette navigation
- Global command palette / global shortcuts that currently fall back to a default workspace id

### 5) Page-by-page behavior (no-workspace mode)
- **Workspace**
  - Navigation entry should go to the existing launcher (`/`).

- **Chat**
  - Message: select a workspace to chat about its cursor, gate state, and artifacts.

- **Plan**
  - Message: select a workspace to view `.aos/spec` and the roadmap/task lenses.

- **Verification**
  - Message: select a workspace to view UAT checks and issues.

- **Runs**
  - Message: select a workspace to view `.aos/evidence/runs`.

- **Continuity**
  - Message: select a workspace to view `.aos/state` and handoff/checkpoints.

- **Codebase**
  - Message: select a workspace to view `.aos/codebase` intelligence.

- **Settings**
  - Message: select a workspace to configure engine/workspace/provider/git settings.

- **Host / Diagnostics** (if routed in no-workspace mode)
  - Message: select a workspace to access engine host controls and diagnostics.

## Risks / edge cases
- Some pages currently derive their internal links from `useWorkspace()` without passing `workspaceId`. Implementation should standardize on using route params when workspace-scoped.
- API-down scenarios can look like "no data"; the no-workspace pages should only appear when **no workspace is in the route**, not when data fetching fails.
- Selecting an empty folder should be distinguished from a truly invalid path or unreadable directory; the UX should offer initialization only when the folder exists and is empty (or otherwise eligible).

## Acceptance tests (manual)
- From `/` click each nav item:
  - Chat/Plan/Verification/Runs/Continuity/Codebase/Settings should go to the corresponding no-workspace route and render the empty state.
- Navigate to a real workspace route (e.g. `/ws/<id>`):
  - nav items should link to workspace-scoped destinations and pages should load as today.

- From `/` select an empty folder:
  - the UI should offer an **Initialize workspace** action (and should not display "Workspace not found").
