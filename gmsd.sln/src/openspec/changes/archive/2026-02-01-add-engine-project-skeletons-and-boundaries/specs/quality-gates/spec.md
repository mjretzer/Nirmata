## ADDED Requirements
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

