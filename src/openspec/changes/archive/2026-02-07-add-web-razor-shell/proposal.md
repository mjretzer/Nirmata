# Change: Add Razor Pages shell with assets and product views

## Why
The nirmata.Web project currently has only a minimal Program.cs with no UI. To provide a user-facing surface for managing product data (Projects), we need a functional Razor Pages application with a cohesive layout, static assets, and read-only pages for listing and viewing project data. This establishes the foundation for the web UI and later agent run status visualization.

## What Changes
- Add Razor Pages folder structure (`Pages/`, `Pages/Shared/`)
- Create layout page with navigation (`_Layout.cshtml`)
- Add static assets pipeline (`wwwroot/css/`, `wwwroot/js/`)
- Implement read-only Project list page (`Pages/Projects/Index.cshtml`)
- Implement read-only Project detail page (`Pages/Projects/Details.cshtml`)
- Configure Razor Pages services in `Program.cs`
- Wire up `IProjectService` dependency injection for page models

## Impact
- Affected specs: `web-razor-pages` (new capability)
- Affected code: `nirmata.Web/Pages/**`, `nirmata.Web/wwwroot/**`, `nirmata.Web/Program.cs`
- Dependencies: Requires `IProjectService` from `nirmata.Services`
