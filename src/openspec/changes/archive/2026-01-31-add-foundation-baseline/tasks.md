## 1. Implementation
- [x] 1.1 Audit existing API/Services/Data/DTO/Common structure and naming; document gaps without renaming or collapsing layers.
  - Notes (gaps observed):
    - Api: No ProblemDetails middleware, validation problem details, API versioning, or health checks wired in `Program.cs`.
    - Api: Swagger/OpenAPI only in Development; no auth hook or consistent error handling pipeline.
    - Services: No explicit transaction boundaries or SaveChanges usage (read-only operations only).
    - Data: `nirmataDbContext` has empty `OnConfiguring` and no design-time factory/migrations baseline.
    - Data: SQLite configuration exists, but no connection string/migration bootstrapping noted.
    - DTO: No DataAnnotations validation attributes on DTOs; no request/response versioning.
    - Common: `nirmata.Common` is empty; no shared primitives (clock, errors, paging, exceptions).
    - Naming/structure: Layered namespaces are consistent (`nirmata.Api`, `nirmata.Services`, `nirmata.Data`, `nirmata.Data.Dto`, `nirmata.Common`) and align with current folders.
- [x] 1.2 Create missing baseline projects and wire them into the solution using existing structure and naming conventions (Web, Aos, Agents, Windows Service, Windows Service API).
- [x] 1.3 Align solution-wide build conventions (target framework, nullable, analyzers, warnings policy).
- [x] 1.4 Add common primitives (error codes, clock, exceptions, paging).
- [x] 1.5 Configure SQLite DbContext and design-time factory; create baseline migration.
- [x] 1.6 Implement minimal data access for the thin-slice entity.
- [x] 1.7 Define service interfaces and transaction boundaries; implement thin-slice service.
- [x] 1.8 Establish API baseline (ProblemDetails, validation, versioning, Swagger, health checks, auth hook) using existing controllers and namespaces.
- [x] 1.9 Implement thin-slice endpoint with DTOs and AutoMapper mappings.
- [x] 1.10 Add unit tests (services) and integration tests (API + SQLite).
- [x] 1.11 Add CI pipeline for build/test on PR.

## 2. Verification
- [x] 2.1 `dotnet build` succeeds for the solution.
- [x] 2.2 `dotnet test` succeeds for unit + integration tests.
- [x] 2.3 Swagger UI loads and `/v1` endpoints are reachable.
- [x] 2.4 Health endpoint returns healthy.
- [x] 2.5 Thin-slice create/read works end-to-end against SQLite.
- [x] 2.6 Validation errors return deterministic ProblemDetails with field-level info.
