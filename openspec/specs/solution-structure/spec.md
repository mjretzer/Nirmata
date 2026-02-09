# solution-structure Specification

## Purpose
TBD - created by archiving change add-foundation-baseline. Update Purpose after archive.
## Requirements
### Requirement: Solution includes baseline projects
The solution SHALL include the baseline project set for product and engine domains using existing structure and naming conventions: `Gmsd.Web`, `Gmsd.Api`, `Gmsd.Services`, `Gmsd.Data`, `Gmsd.Data.Dto`, `Gmsd.Common`, `Gmsd.Aos`, `Gmsd.Agents`, `Gmsd.Windows.Service`, and `Gmsd.Windows.Service.Api`.

The solution SHALL preserve the intended bring-up order and separation of concerns:
- Shared: `Gmsd.Common`
- Engine libraries: `Gmsd.Aos` → `Gmsd.Agents`
- Product stack: `Gmsd.Data.Dto` → `Gmsd.Data` → `Gmsd.Services` → `Gmsd.Api` → `Gmsd.Web`
- Engine hosts: `Gmsd.Windows.Service` → `Gmsd.Windows.Service.Api` (may remain skeletons until later milestones)

The solution SHALL treat the Engine (AOS) and Product Application as distinct dependency planes:
- Engine libraries MUST remain product-independent.
- Product projects MUST NOT depend on engine workflow internals.

#### Scenario: Baseline projects exist in the solution
- **WHEN** the solution is opened or built
- **THEN** each baseline project is present in the solution with a corresponding `.csproj` and source folder

