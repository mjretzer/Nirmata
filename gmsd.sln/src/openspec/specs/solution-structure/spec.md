# solution-structure Specification

## Purpose

Defines the repository solution/project structure and domain separation boundaries.

- **Lives in:** Solution root (`*.sln`) and project directories (`Gmsd.*/*`)
- **Owns:** Project ownership boundaries and allowed dependency direction
- **Does not own:** Feature-level workflow semantics (covered by capability specs)
## Requirements
### Requirement: Solution includes baseline projects
The solution SHALL include the baseline project set for product and engine domains using existing structure and naming conventions: `Gmsd.Web`, `Gmsd.Api`, `Gmsd.Services`, `Gmsd.Data`, `Gmsd.Data.Dto`, `Gmsd.Common`, `Gmsd.Aos`, `Gmsd.Agents`, `Gmsd.Windows.Service`, and `Gmsd.Windows.Service.Api`.

The solution SHALL preserve the intended bring-up order and separation of concerns:
- Shared: `Gmsd.Common`
- Engine libraries: `Gmsd.Aos` â†’ `Gmsd.Agents`
- Product stack: `Gmsd.Data.Dto` â†’ `Gmsd.Data` â†’ `Gmsd.Services` â†’ `Gmsd.Api` â†’ `Gmsd.Web`
- Engine hosts: `Gmsd.Windows.Service` â†’ `Gmsd.Windows.Service.Api` (may remain skeletons until later milestones)

The solution SHALL treat the Engine (AOS) and Product Application as distinct dependency planes:
- Engine libraries MUST remain product-independent.
- Product projects MUST NOT depend on engine workflow internals.

#### Scenario: Baseline projects exist in the solution
- **WHEN** the solution is opened or built
- **THEN** each baseline project is present in the solution with a corresponding `.csproj` and source folder

