## ADDED Requirements
### Requirement: Solution includes baseline projects
The solution SHALL include the baseline project set for product and engine domains using existing structure and naming conventions: `Gmsd.Web`, `Gmsd.Api`, `Gmsd.Services`, `Gmsd.Data`, `Gmsd.Data.Dto`, `Gmsd.Common`, `Gmsd.Aos`, `Gmsd.Agents`, `Gmsd.Windows.Service`, and `Gmsd.Windows.Service.Api`.

#### Scenario: Baseline projects exist in the solution
- **WHEN** the solution is opened or built
- **THEN** each baseline project is present in the solution with a corresponding `.csproj` and source folder
