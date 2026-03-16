# solution-structure Specification

## Purpose

Defines the repository solution/project structure and domain separation boundaries.

- **Lives in:** Solution root (`*.sln`) and project directories (`nirmata.*/*`)
- **Owns:** Project ownership boundaries and allowed dependency direction
- **Does not own:** Feature-level workflow semantics (covered by capability specs)
## Requirements
### Requirement: Solution includes baseline projects
The solution SHALL include the baseline project set for product and engine domains using existing structure and naming conventions: `nirmata.Web`, `nirmata.Api`, `nirmata.Services`, `nirmata.Data`, `nirmata.Data.Dto`, `nirmata.Common`, `nirmata.Aos`, `nirmata.Agents`, `nirmata.Windows.Service`, and `nirmata.Windows.Service.Api`.

The solution SHALL preserve the intended bring-up order and separation of concerns:
- Shared: `nirmata.Common`
- Engine libraries: `nirmata.Aos` → `nirmata.Agents`
- Product stack: `nirmata.Data.Dto` → `nirmata.Data` → `nirmata.Services` → `nirmata.Api` → `nirmata.Web`
- Engine hosts: `nirmata.Windows.Service` → `nirmata.Windows.Service.Api` (may remain skeletons until later milestones)

The solution SHALL treat the Engine (AOS) and Product Application as distinct dependency planes:
- Engine libraries MUST remain product-independent.
- Product projects MUST NOT depend on engine workflow internals.

#### Scenario: Baseline projects exist in the solution
- **WHEN** the solution is opened or built
- **THEN** each baseline project is present in the solution with a corresponding `.csproj` and source folder

