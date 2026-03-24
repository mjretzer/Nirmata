# Tasks: No-workspace detected pages

## 1) Add shared no-workspace empty state component
- [x] Create a shared UI component (e.g. `NoWorkspaceDetected`) with title/description/CTA.
- [x] Ensure the component supports page-specific copy (prop-driven).

## 2) Add no-workspace routes
- [x] Update `nirmata.frontend/src/app/router.tsx` to add top-level routes:
  - `/chat`, `/plan`, `/verification`, `/runs`, `/continuity`, `/codebase`, `/settings`
  - (Optional) `/host`, `/diagnostics`
- [x] Implement corresponding page components that render `NoWorkspaceDetected` with appropriate copy.

Note: these routes must render **only** the blank-state UI (do not mount the existing workspace-scoped page components behind them).

## 3) Update navigation + breadcrumbs to avoid fake workspace ids
- [x] Update sidebar navigation generation so that when the app is not under `/ws/:workspaceId/...`, nav items link to the no-workspace routes instead of workspace-scoped URLs.
- [x] Update `TopRibbon` workspace-id derivation similarly so breadcrumbs do not imply a workspace when none is selected.

Also update other entry points that currently fall back to a default workspace id:
- [x] Update `TopRibbon` embedded command-palette navigation to use no-workspace routes when unscoped.
- [x] Update `nirmata.frontend/src/app/components/layout/GlobalCommandPalette.tsx` so it does not navigate to `/ws/:workspaceId/...` when there is no workspace in the URL.

## 4) Verify workspace-scoped behavior is unchanged
- [x] Confirm that under `/ws/:workspaceId/...` all nav items still link to workspace-scoped pages.
- [x] Confirm that page rendering and file-explorer behavior remain intact.

## 5) Empty-folder selection should prompt workspace initialization
- [x] When a user selects a valid but empty folder from the launcher, show an **Initialize workspace** prompt instead of "Workspace not found".
- [x] Ensure truly invalid/unreadable paths still show an error state (do not offer initialization).

## 6) UX polish + regression checks
- [x] Ensure each no-workspace page has:
  - clear title
  - short description
  - primary CTA back to `/`
- [x] Confirm no console renders with empty workspace ids (no `/ws//...` navigation).

Manual acceptance checks:
- [x] From `/` click each sidebar nav item:
  - Chat/Plan/Verification/Runs/Continuity/Codebase/Settings should go to the corresponding no-workspace route and show the blank state.
  - Confirm the original console UI does not render in this mode.
- [x] From `/` select an empty folder:
  - should offer an **Initialize workspace** action (and should not display "Workspace not found").
- [x] Under `/ws/:workspaceId/...`:
  - sidebar links and command palettes produce workspace-scoped URLs and behavior remains unchanged.
