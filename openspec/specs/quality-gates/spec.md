# quality-gates Specification

## Purpose
TBD - created by archiving change add-foundation-baseline. Update Purpose after archive.
## Requirements
### Requirement: Solution Build Conventions
The solution SHALL enforce consistent build conventions (target framework, nullable, analyzers, warnings policy).

#### Scenario: Build configuration
- **WHEN** the solution is built
- **THEN** the build uses the defined conventions without ad-hoc per-project overrides

### Requirement: Unit Test Harness
The solution SHALL include a unit test project that exercises service-layer behavior.

#### Scenario: Service unit test
- **WHEN** the unit tests run
- **THEN** at least one service behavior is validated without touching the database

### Requirement: Integration Test Harness
The solution SHALL include integration tests that exercise the API against SQLite.

#### Scenario: End-to-end test
- **WHEN** integration tests run
- **THEN** the thin-slice endpoint is tested end-to-end with SQLite

### Requirement: CI Build and Test Pipeline
The repository SHALL run build and test steps on each push or pull request.

#### Scenario: Pull request
- **WHEN** a pull request is opened
- **THEN** the pipeline runs `dotnet build` and `dotnet test`

### Requirement: Project reference boundaries are enforced at build time
The solution SHALL fail the build when a project reference violates the dependency direction defined in `openspec/project.md`:

- Engine projects (`Gmsd.Aos`, `Gmsd.Agents`, and engine hosts) MUST NOT reference Product Application projects (`Gmsd.Data.Dto`, `Gmsd.Data`, `Gmsd.Services`, `Gmsd.Api`, `Gmsd.Web`).
- Product Application projects MUST NOT reference engine workflow internals (`Gmsd.Aos`, `Gmsd.Agents`).
- Product Application projects MUST reference only lower layers in the product stack order (`Gmsd.Data.Dto` → `Gmsd.Data` → `Gmsd.Services` → `Gmsd.Api` → `Gmsd.Web`).

#### Scenario: Engine does not reference Product
- **WHEN** an engine project adds a `ProjectReference` to a Product Application project
- **THEN** the build fails with an error explaining the forbidden dependency edge

#### Scenario: Product does not reference engine internals
- **WHEN** a Product Application project adds a `ProjectReference` to `Gmsd.Aos` or `Gmsd.Agents`
- **THEN** the build fails with an error explaining the forbidden dependency edge

#### Scenario: Product layer order is enforced
- **WHEN** a Product Application project adds a `ProjectReference` to a higher product layer
- **THEN** the build fails with an error explaining the forbidden dependency edge

### Requirement: AOS engine fixture/snapshot regression coverage
The repository SHALL include deterministic fixture/snapshot regression coverage for the AOS engine so that nondeterministic drift is detected automatically.

#### Scenario: CI fails on drift
- **WHEN** `dotnet test` runs in CI
- **THEN** AOS engine fixture/snapshot regression tests run and fail if produced outputs differ from the approved fixtures

### Requirement: AOS public API boundary is enforced at build time
The solution SHALL fail the build when the `Gmsd.Aos` public API boundary is violated:

- Consumers MUST NOT compile against internal AOS engine namespaces (e.g., `Gmsd.Aos.Engine.*`, `Gmsd.Aos._Shared.*`).
- The `Gmsd.Aos` public surface (`Gmsd.Aos.Public.*`) MUST NOT expose internal engine types through public members.

#### Scenario: Consumer cannot compile against engine internals
- **WHEN** a consumer project attempts to reference a type from `Gmsd.Aos.Engine.*` or `Gmsd.Aos._Shared.*`
- **THEN** the build fails with an actionable error indicating the forbidden dependency

#### Scenario: Public API does not leak internal types
- **WHEN** the `Gmsd.Aos` assembly is built and its public API is evaluated
- **THEN** no public member signature references a type in `Gmsd.Aos.Engine.*` or `Gmsd.Aos._Shared.*`

