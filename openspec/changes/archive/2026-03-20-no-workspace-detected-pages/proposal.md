# Proposal: No-workspace detected pages

## Problem
The frontend assumes a workspace is always selected.

- Routes and navigation are predominantly workspace-scoped (`/ws/:workspaceId/...`).
- When the user is on the launcher (`/`) there is **no real workspace selection**, but layout code currently derives a fallback workspace id (ex: `"my-app"`).
- Several pages/components call workspace hooks without an explicit `workspaceId` and then derive URLs from an empty/default workspace object. This leads to blank/odd UI states and confusing navigation.

Additionally, when a user selects a valid but **empty folder**, the app currently reports **"Workspace not found"** instead of prompting to initialize a new workspace in that folder.

The UX gap: when no workspace is selected, the main navigation still allows entry into workspace-centric consoles (Chat/Plan/Verification/Runs/Continuity/Codebase/Settings), but there is no consistent "No workspace detected" state.

## Goals
- Provide a consistent, explicit **"No workspace detected"** empty state for each main navigation page when the app is not currently scoped to a workspace.
- Make navigation behavior predictable from the launcher state (no fake workspace id).
- Ensure pages do not render partially-populated UI based on empty/default workspace data.
- When the user selects an **empty folder**, prompt them to **initialize a new workspace** in that folder (instead of showing "Workspace not found").

In no-workspace mode, the destination pages should be **overwritten** by the empty state (i.e. do not mount the normal console pages and do not show partially-initialized UI).

## Non-goals
- Implement full workspace CRUD or selection persistence.
- Change API behavior or add new endpoints.
- Refactor existing workspace-scoped page functionality beyond adding gating/empty states.

## Scope
Main navigation pages:
- Workspace (launcher/dashboard entry)
- Chat
- Plan
- Verification
- Runs
- Continuity
- Codebase
- Settings

Secondary pages linked from Settings:
- Host Console
- Diagnostics

## Success criteria
- When there is **no workspace in the URL** (not under `/ws/:workspaceId/...`), navigating to any main console shows a clear "No workspace detected" page with an action to go to the Workspace Launcher.
- When a workspace is selected (under `/ws/:workspaceId/...`), existing pages behave as they do today.
- No runtime errors or broken links occur due to empty workspace ids.

- When the user selects an **empty folder** from the launcher, the UI offers an **Initialize workspace** action rather than showing "Workspace not found".

Additionally:
- The app must not fabricate a workspace id (e.g. defaulting to `"my-app"`) when unscoped; navigation entry points should land on the new unscoped routes (e.g. `/chat`, `/plan`, ...) so the blank-state pages are actually reachable from `/`.
