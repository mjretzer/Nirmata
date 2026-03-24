## Context

The Nirmata backend currently has a foundational ASP.NET Core API structure but lacks fully implemented project management endpoints and standardized health monitoring. Swagger is partially configured but needs verification and potential refinement to ensure all new endpoints are correctly documented.

## Goals / Non-Goals

**Goals:**
- Implement a complete set of CRUD and search endpoints for `Project` entities.
- Provide a detailed health check endpoint that verifies database connectivity.
- Ensure Swagger UI correctly reflects all available endpoints and their data contracts.
- Maintain consistency with existing architecture (Controllers -> Services -> Repositories).

**Non-Goals:**
- Implementing complex project-specific logic (e.g., resource allocation, detailed scheduling).
- Adding authentication/authorization in this specific change (assuming it's handled separately or later).
- Frontend integration (this is backend only).

## Decisions

- **Controller vs Minimal APIs**: Use Controllers for `ProjectController` to maintain consistency with existing structure and better organization for standard CRUD. Use Minimal API or simple Controller for Health check depending on complexity.
- **DTOs**: Use separate Request/Response DTOs in `nirmata.Data.Dto` to decouple API contracts from internal data models.
- **Validation**: Implement FluentValidation for incoming requests in `nirmata.Data.Dto/Validators`.
- **Dependency Injection**: Register all new services and repositories in `nirmata.Services/Composition/ServiceCollectionExtensions.cs`.

## Risks / Trade-offs

- **[Risk]** API breaking changes if internal models change → **[Mitigation]** Use DTOs for all public endpoints.
- **[Risk]** Swagger configuration mismatch → **[Mitigation]** Regular manual verification at `/swagger` during development.
- **[Risk]** Database health check latency → **[Mitigation]** Use standard EF Core health check provider with reasonable timeouts.
