## ADDED Requirements
### Requirement: Solution includes baseline projects
The solution SHALL include the baseline project set for product and engine domains using existing structure and naming conventions: `nirmata.Web`, `nirmata.Api`, `nirmata.Services`, `nirmata.Data`, `nirmata.Data.Dto`, `nirmata.Common`, `nirmata.Aos`, `nirmata.Agents`, `nirmata.Windows.Service`, and `nirmata.Windows.Service.Api`.

#### Scenario: Baseline projects exist in the solution
- **WHEN** the solution is opened or built
- **THEN** each baseline project is present in the solution with a corresponding `.csproj` and source folder
