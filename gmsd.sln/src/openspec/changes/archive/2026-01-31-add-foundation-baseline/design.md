## Context
The solution is a layered .NET 10 stack with ASP.NET Core, EF Core 9 + SQLite, and AutoMapper. The repository already contains a basic API foundation (controllers, services, data, DTOs, and models such as Project/Step), and the goal is to lock foundational boundaries and behaviors so feature work can proceed without architectural churn.

## Goals / Non-Goals
- Goals:
  - Define stable boundaries between Api, Services, Data, DTO, and Common.
  - Establish a thin-slice endpoint that proves end-to-end wiring.
  - Standardize error handling, validation, logging, and health checks.
  - Provide a repeatable database workflow and test/CI gates.
- Non-Goals:
  - Full product functionality beyond the thin slice.
  - Premature repository or unit-of-work abstraction.
  - Authentication/authorization implementation (hook point only).

## Decisions
- Decision: Use ASP.NET Core `ProblemDetails` as the canonical error response format.
  - Alternatives considered: custom `Result<T>` envelope.
  - Rationale: native integration with exception handling and validation; minimizes custom plumbing.
- Decision: Use DataAnnotations for request validation in DTOs.
  - Alternatives considered: FluentValidation.
  - Rationale: consistent with ASP.NET Core defaults and low ceremony for the foundation.
- Decision: Use structured logging via built-in `ILogger` with JSON console output and include request correlation via trace/activity id.
  - Alternatives considered: Serilog with enrichers.
  - Rationale: keep dependencies minimal while still structured and queryable.
- Decision: Keep mapping in `Gmsd.Data.Mapping` using AutoMapper profiles.
  - Alternatives considered: manual mapping in services/controllers.
  - Rationale: aligns with existing conventions and keeps transformations centralized.
- Decision: Preserve existing project names and namespaces (`Gmsd.Api`, `Gmsd.Services`, `Gmsd.Data`, `Gmsd.Data.Dto`, `Gmsd.Common`) while extending capabilities.
  - Alternatives considered: renaming or merging layers.
  - Rationale: current nomenclature already reflects concern separation and should be extended, not replaced.
- Decision: Services own transaction boundaries and call `SaveChanges` for atomic operations.
  - Alternatives considered: controller-level unit of work or repository pattern.
  - Rationale: keeps API thin and aligns with service-centric business logic.
- Decision: Use a GitHub Actions pipeline for build/test automation.
  - Alternatives considered: Azure DevOps pipeline.
  - Rationale: default assumption; can be swapped if repository hosting differs.

## Risks / Trade-offs
- DataAnnotations may be limiting for complex validation rules; can migrate to FluentValidation later.
- ProblemDetails enforces a standard envelope but requires consistent exception translation to avoid leaking internals.
- JSON console logging may be less feature-rich than dedicated logging packages.

## Migration Plan
1. Add baseline specs and tasks to formalize the foundation.
2. Implement the thin slice and cross-cutting behaviors.
3. Add tests and CI automation to lock the baseline.

## Open Questions
- None. CI provider is GitHub Actions and the health route is `/health`.
