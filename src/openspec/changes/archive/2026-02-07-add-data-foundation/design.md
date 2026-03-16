## Context
Establishing the product data persistence layer for nirmata. This is foundational work that enables the product application (Projects/Steps domain) to store and retrieve data independently of the AOS engine workspace.

## Goals / Non-Goals
- **Goals:**
  - Clean EF Core DbContext for product domain
  - SQLite for local/dev scenarios
  - Lazy-loading proxies for convenient navigation
  - Proper entity relationships (Project → Steps cascade delete)
  - Migration baseline for schema evolution
- **Non-Goals:**
  - Production database configuration (SQL Server/PostgreSQL)
  - Complex query optimization
  - Repository pattern abstraction (deferred to future work)

## Decisions
- **Decision:** Use SQLite with EF Core for initial development
  - **Rationale:** Zero-config local development, single-file database, easy to reset
  - **Alternatives considered:** In-memory (not persistent), SQL Server (requires setup)

- **Decision:** Enable lazy-loading proxies
  - **Rationale:** Simplifies navigation property access without explicit Include()
  - **Trade-off:** Slight performance overhead, requires virtual navigation properties

- **Decision:** Use string IDs for entities
  - **Rationale:** Flexible identifier generation, consistent with AOS workspace patterns

## Risks / Trade-offs
- **Risk:** SQLite limitations (no stored procedures, limited concurrency)
  - **Mitigation:** Documented; future migration path to SQL Server/PostgreSQL exists via EF Core

## Migration Plan
N/A — this is foundational schema creation, not a migration of existing data.

## Open Questions
- None at this time.
