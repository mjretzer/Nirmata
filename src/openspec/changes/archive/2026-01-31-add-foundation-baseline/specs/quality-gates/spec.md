## ADDED Requirements
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
