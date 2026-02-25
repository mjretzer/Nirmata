# Change: Add Core AOS UI Pages

## Why
The AOS engine requires a minimum UI surface to be operational end-to-end. Currently `Gmsd.Web` only has basic project management pages and a runs dashboard. The missing operational pages (workspace picker, dashboard overview, command center, specs explorer, and project spec editor) block users from interacting with the `.aos/` workspace effectively.

## What Changes
- **ADDED** Workspace Picker page (`/Workspace`) for `.aos/` directory selection and health checks
- **ADDED** Dashboard page (`/Dashboard`) showing current cursor position, blockers, and recommended actions  
- **ADDED** Command Center page (`/Command`) with modern chat UI and slash commands
- **ADDED** Specs Explorer page (`/Specs`) for browsing and editing AOS artifacts
- **ADDED** Project Spec page (`/Specs/Project`) for editing `spec/project.json` with interview mode
- **MODIFIED** Navigation layout to link the new pages

## Impact
- **Affected specs:** `web-workspace` (new), `web-dashboard` (new), `web-command-center` (new), `web-specs-explorer` (new), `web-project-spec` (new)
- **Affected code:** `Gmsd.Web/Pages/Workspace/**`, `Gmsd.Web/Pages/Dashboard/**`, `Gmsd.Web/Pages/Command/**`, `Gmsd.Web/Pages/Specs/**`, `Gmsd.Web/Pages/Shared/_Layout.cshtml`
