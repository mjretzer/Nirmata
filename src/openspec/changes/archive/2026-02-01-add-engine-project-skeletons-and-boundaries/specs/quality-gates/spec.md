## ADDED Requirements
### Requirement: Project reference boundaries are enforced at build time
The solution SHALL fail the build when a project reference violates the dependency direction defined in `openspec/project.md`:

- Engine projects (`nirmata.Aos`, `nirmata.Agents`, and engine hosts) MUST NOT reference Product Application projects (`nirmata.Data.Dto`, `nirmata.Data`, `nirmata.Services`, `nirmata.Api`, `nirmata.Web`).
- Product Application projects MUST NOT reference engine workflow internals (`nirmata.Aos`, `nirmata.Agents`).
- Product Application projects MUST reference only lower layers in the product stack order (`nirmata.Data.Dto` → `nirmata.Data` → `nirmata.Services` → `nirmata.Api` → `nirmata.Web`).

#### Scenario: Engine does not reference Product
- **WHEN** an engine project adds a `ProjectReference` to a Product Application project
- **THEN** the build fails with an error explaining the forbidden dependency edge

#### Scenario: Product does not reference engine internals
- **WHEN** a Product Application project adds a `ProjectReference` to `nirmata.Aos` or `nirmata.Agents`
- **THEN** the build fails with an error explaining the forbidden dependency edge

#### Scenario: Product layer order is enforced
- **WHEN** a Product Application project adds a `ProjectReference` to a higher product layer
- **THEN** the build fails with an error explaining the forbidden dependency edge

