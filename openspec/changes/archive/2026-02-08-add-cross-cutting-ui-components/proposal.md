# Change: Add Cross-Cutting UI Components

## Why
The GMSD Web UI currently has pages with good functionality but lacks consistent cross-cutting UX patterns. Users need a global command palette for quick navigation, persistent workspace context visibility, unified artifact linking, and toast notifications for system feedback. These components will create a cohesive, professional experience across all pages.

## What Changes
- **ADDED** Global Command Palette (Ctrl+K) — jump to any artifact, run, command, or page
- **ADDED** Persistent Workspace Badge — shows current repo path + .aos health status in header
- **ADDED** Unified Artifact Link System — TSK/PH/MS/UAT/RUN prefixes become clickable links
- **ADDED** Toast/Notification System — validation failures, run completion, lock conflicts

## Impact
- **Affected specs:** `web-cross-cutting-components` (new)
- **Affected code:** `Gmsd.Web/Pages/Shared/_Layout.cshtml`, `Gmsd.Web/wwwroot/js/site.js`, `Gmsd.Web/wwwroot/css/site.css`
- **Dependencies:** Requires existing `.aos/` workspace structure for health checks
- **Non-breaking:** All changes are additive UI features; no API or schema changes
