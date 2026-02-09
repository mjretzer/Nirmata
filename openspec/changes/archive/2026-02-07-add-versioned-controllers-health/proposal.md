# Change: Add Versioned Controllers and Detailed Health Endpoint

## Why
The API has basic infrastructure in place (v1 routing, basic health, swagger) but lacks:
1. A detailed health endpoint that reports dependency status (DB connectivity)
2. A base controller class for shared API behaviors
3. Comprehensive integration tests covering health and v1 endpoints

This change completes the stable product API foundation with proper observability and test coverage.

## What Changes
- **ADDED** `HealthController.cs` with detailed health checks (DB connectivity, service status)
- **ADDED** `GmsdController.cs` base class for shared controller behaviors
- **MODIFIED** `api-foundation` spec to require detailed health reporting
- **ADDED** Integration tests for health endpoints in `Gmsd.Api.Tests`

## Impact
- **Affected specs:** `api-foundation`
- **Affected code paths:**
  - `Gmsd.Api/Controllers/HealthController.cs` (new)
  - `Gmsd.Api/Controllers/GmsdController.cs` (new)
  - `Gmsd.Api/Controllers/V1/ProjectController.cs` (refactored to use base)
  - `tests/Gmsd.Api.Tests/` (new health tests)
- **Breaking change:** No — additive only

## Dependencies
- Relies on existing `Gmsd.Services` for service health indicators
- Relies on `Gmsd.Data` for DB health checks
