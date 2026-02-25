# api-foundation Specification

## Purpose

Defines product API surface conventions and behavior for $capabilityId.

- **Lives in:** `Gmsd.Api/*`, `Gmsd.Services/*`, `Gmsd.Data.Dto/*`
- **Owns:** HTTP API shape, request/response DTO boundaries, and API-layer conventions
- **Does not own:** Agent orchestration engine mechanics or workflow control loops
## Requirements
### Requirement: Versioned Routing and OpenAPI
The API SHALL expose endpoints under a versioned route prefix (e.g., `/v1`) and publish OpenAPI documentation.

#### Scenario: Swagger availability
- **WHEN** the API is running locally
- **THEN** Swagger UI is reachable and documents the `/v1` endpoints

### Requirement: Global Exception Handling
The API SHALL translate unhandled exceptions into `ProblemDetails` responses with correlation information.

#### Scenario: Unhandled exception
- **WHEN** an unhandled exception occurs during a request
- **THEN** the response is a 500 `ProblemDetails` payload containing a trace or correlation id

### Requirement: Validation Pipeline
The API SHALL perform deterministic request validation and return field-level errors in `ProblemDetails`.

#### Scenario: Invalid request
- **WHEN** a request DTO violates validation rules
- **THEN** the API returns a 400 `ProblemDetails` with field-level errors

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

### Requirement: Thin-Slice Project Endpoints
The API SHALL expose a minimal Project thin-slice under `/v1/projects` with create and read-by-id endpoints.

#### Scenario: Create project
- **WHEN** a client POSTs a valid Project create request to `/v1/projects`
- **THEN** the API returns `201 Created` with the Project response DTO

#### Scenario: Read project by id
- **WHEN** a client GETs `/v1/projects/{projectId}`
- **THEN** the API returns the Project response DTO or `404` if not found

### Requirement: Controller Base Class
The API SHALL provide a base controller class that encapsulates shared API behaviors and consistent response patterns.

#### Scenario: Controller inheritance
- **WHEN** a controller inherits from `GmsdController`
- **THEN** it has access to common response helpers and consistent API behaviors

#### Scenario: Consistent API responses
- **WHEN** any controller action returns a result
- **THEN** the response follows consistent formatting and HTTP status code conventions

### Requirement: Auth Hook Point
The API SHALL include a minimal authentication/authorization hook point without implementing full auth.

#### Scenario: Auth enabled
- **WHEN** authentication is configured
- **THEN** unauthorized requests return 401 consistently

### Requirement: Structured Logging
The API SHALL emit structured logs that include per-request correlation identifiers.

#### Scenario: Request logging
- **WHEN** a request is processed
- **THEN** logs include the trace or correlation id for that request

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

