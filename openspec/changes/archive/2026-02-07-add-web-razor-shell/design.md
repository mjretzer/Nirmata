## Context
Gmsd.Web is the user-facing web UI for the GMSD platform. Currently it has only a minimal Program.cs stub. This change establishes the Razor Pages foundation with a clean architecture that separates page models (logic) from views (presentation).

## Goals / Non-Goals
- Goals:
  - Provide functional read-only UI for Project data
  - Establish consistent layout and navigation patterns
  - Create maintainable static asset organization
  - Follow ASP.NET Core Razor Pages conventions

- Non-Goals:
  - Create/Edit/Delete operations (out of scope for this change)
  - Client-side SPA frameworks (keep it server-rendered Razor)
  - Authentication/authorization (future change)
  - Agent run status visualization (future change, PH-PRD-0006)

## Decisions
- **Page Model Pattern**: Use async page handlers (`OnGetAsync`) with injected services for data access
- **Service Integration**: Inject `IProjectService` directly into page models (allowed per project.md: Gmsd.Web → Gmsd.Services reference is valid)
- **Error Handling**: Catch `NotFoundException` from service layer and return 404 via `NotFound()` result
- **Static Assets**: Minimal custom CSS in `site.css`, no external frameworks needed for read-only tables

## Risks / Trade-offs
- Tight coupling to `IProjectService` → Mitigation: This is intentional per dependency rules; future API client mode would wrap the same interface
- No pagination on list view → Mitigation: Acceptable for initial implementation; document for future enhancement

## Migration Plan
Not applicable - this is new functionality with no existing UI to migrate.

## Open Questions
- [ ] Should we include basic responsive styling (mobile-friendly tables)? (Recommended: Yes, minimal effort)
