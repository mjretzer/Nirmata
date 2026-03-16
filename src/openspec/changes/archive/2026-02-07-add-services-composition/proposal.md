# Change: Add Services Composition Root and DTO Mapping

## Why
The current service layer returns entities directly instead of DTOs, violating the established mapping policy. Additionally, service registration is scattered in `nirmata.Api/Program.cs` rather than centralized in a composition root. This proposal establishes proper service boundaries by:
1. Moving service registration to a dedicated composition extension method
2. Updating service interfaces to return DTOs (using AutoMapper for mapping)
3. Adding service-level tests to verify the implementation

## What Changes
- **ADDED** `nirmata.Services/Composition/ServiceCollectionExtensions.cs` with `AddnirmataServices()` extension method
- **ADDED** Service-level tests in `nirmata.Services.Tests/` project
- **MODIFIED** `IProjectService` interface to return DTOs (`ProjectDto`, `ProjectResponseDto`) instead of entities
- **MODIFIED** `ProjectService` implementation to use AutoMapper for entity-to-DTO mapping
- **MODIFIED** `nirmata.Api/Program.cs` to use the new `AddnirmataServices()` extension method

## Impact
- **Affected specs:** `services-foundation`
- **Affected code paths:**
  - `nirmata.Services/Composition/**` (new)
  - `nirmata.Services/Interfaces/**` (modified)
  - `nirmata.Services/Implementations/**` (modified)
  - `nirmata.Api/Program.cs` (refactored)
- **Breaking change:** Yes - API returns DTOs instead of entities; controllers using these services may need adjustment

## Dependencies
- Relies on existing `nirmata.Data/Mapping/MappingProfile` for AutoMapper configuration
- Relies on existing DTOs in `nirmata.Data.Dto`
