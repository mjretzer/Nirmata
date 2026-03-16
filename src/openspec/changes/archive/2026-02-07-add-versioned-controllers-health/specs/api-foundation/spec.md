## MODIFIED Requirements

### Requirement: Health Checks
The API SHALL provide a health endpoint that reports service health and dependency status (database connectivity).

#### Scenario: Basic health probe
- **WHEN** `/health` is requested
- **THEN** the API returns a healthy response when the service is running

#### Scenario: Detailed health check
- **WHEN** `/api/health` is requested
- **THEN** the API returns a JSON response with:
  - Overall service status (Healthy/Degraded/Unhealthy)
  - Database connectivity status
  - Response time metrics

### Requirement: Controller Base Class
The API SHALL provide a base controller class that encapsulates shared API behaviors and consistent response patterns.

#### Scenario: Controller inheritance
- **WHEN** a controller inherits from `nirmataController`
- **THEN** it has access to common response helpers and consistent API behaviors

#### Scenario: Consistent API responses
- **WHEN** any controller action returns a result
- **THEN** the response follows consistent formatting and HTTP status code conventions

## ADDED Requirements

### Requirement: Health Controller
The API SHALL expose a dedicated `HealthController` providing detailed health diagnostics beyond the basic health check endpoint.

#### Scenario: Health endpoint availability
- **WHEN** a client GETs `/api/health`
- **THEN** the controller returns detailed health information including dependency checks

#### Scenario: Database health verification
- **WHEN** the health endpoint checks database connectivity
- **THEN** it reports whether the database is reachable and responsive

### Requirement: Integration Test Coverage
The API SHALL have integration tests covering health endpoints and v1 controller endpoints with in-memory database.

#### Scenario: Health endpoint test
- **WHEN** integration tests run
- **THEN** a test verifies the health endpoint returns 200 OK with valid health data

#### Scenario: V1 endpoints test
- **WHEN** integration tests run
- **THEN** tests verify ProjectController v1 endpoints return correct HTTP status codes and data
