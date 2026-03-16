## Context
nirmata is intentionally split into two major domains:

- **Product Application**: `nirmata.Data.Dto` → `nirmata.Data` → `nirmata.Services` → `nirmata.Api` → `nirmata.Web`
- **Engine (AOS)**: `nirmata.Aos` → `nirmata.Agents` (plus host processes in later milestones)

The solution already centralizes build conventions in `Directory.Build.props`, but it does not yet codify and *enforce* project reference boundaries.

The roadmap phase `add-engine-project-skeletons-and-boundaries` requires that the boundary is proven at solution-level (i.e., it is not “by convention”).

## Goals / Non-Goals
### Goals
- Enforce compile-time dependency direction:
  - Engine projects MUST NOT reference Product projects.
  - Product projects MUST NOT reference engine workflow internals (i.e., `nirmata.Aos` / `nirmata.Agents`).
- Enforce Product stack reference order (DTO → Data → Services → API → Web).
- Make violations fail the build in CI and locally.

### Non-Goals
- Introduce Roslyn analyzers or third-party architecture enforcement tooling (keep it minimal).
- Define runtime hosting behavior for `nirmata.Windows.Service*` (these can remain skeletons until later milestones).

## Decisions
### Decision: Use MSBuild-based enforcement (solution-level)
Implement the boundary checks as a solution-level MSBuild target (e.g., `Directory.Build.targets`) that inspects `ProjectReference` items during build and errors on forbidden reference edges.

This is preferred because:
- It is **build-native** and runs anywhere `dotnet build` runs (including CI).
- It is centralized and does not require custom test runners to enforce boundaries.
- It aligns with “failing build if violated” exit criteria.

### Decision: Define project layers via a centralized mapping
Define a canonical “layer” classification per project name (e.g., `nirmataLayer=Engine|Product|Host|Shared` plus sub-layers for the Product stack).

Prefer implementing the mapping centrally (e.g., `Directory.Build.props` keyed by `$(MSBuildProjectName)`) to avoid requiring every `.csproj` to duplicate metadata.

## Alternatives Considered
- **Test-only enforcement** (parse solution/project files and assert rules): simpler to implement, but easier to bypass and less obviously “build-time”.
- **Roslyn analyzers**: powerful, but adds complexity and maintenance early.
- **External tooling** (e.g., architecture tests/packages): increases dependency surface and needs evaluation.

## Risks / Trade-offs
- MSBuild logic can be opaque if errors are not clear → mitigate with explicit, actionable error messages that name both projects and the forbidden rule.
- The checks will primarily validate **direct** `ProjectReference` edges → acceptable because layer boundaries are expressed via project references, and direct enforcement prevents new coupling.

## Migration Plan
- Add the MSBuild enforcement.
- Fix any existing project references that violate the rules (if present).
- Ensure CI uses `dotnet build "nirmata.slnx"` and fails on violations.

