## Why

Standardize and expose backend functionality through a consistent HTTP API with interactive documentation. This enables frontend development, external integrations, and simplifies testing by providing a clear contract for project-related operations and health monitoring.

## What Changes

- **New Endpoints**: Implementation of standard RESTful endpoints for project management (CRUD and search).
- **Swagger Integration**: Automated OpenAPI specification generation and UI for endpoint discovery and testing.
- **Health Monitoring**: Detailed health checks including database connectivity.
- **Service Layer Expansion**: New business logic in `ProjectService` to support API requirements.
- **Data Access Refinement**: Enhanced `ProjectRepository` for data retrieval and persistence.

## Capabilities

### New Capabilities
- `project-management`: Core CRUD operations for projects, including detailed retrieval and search functionality.
- `system-health`: Comprehensive health monitoring endpoints for the API and its dependencies (e.g., Database).

### Modified Capabilities
- None: This establishes the initial API surface area for these domains.

## Impact

- **API**: New `V1` controllers and health endpoints in `nirmata.Api`.
- **Services**: `IProjectService` and its implementation in `nirmata.Services`.
- **Data**: Repository updates and `nirmataDbContext` usage.
- **Documentation**: Swagger UI accessible at `/swagger`.
