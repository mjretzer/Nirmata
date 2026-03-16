## ADDED Requirements
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
The API SHALL provide a health endpoint that reports service health.

#### Scenario: Health probe
- **WHEN** `/health` is requested
- **THEN** the API returns a healthy response when dependencies are available

### Requirement: Thin-Slice Project Endpoints
The API SHALL expose a minimal Project thin-slice under `/v1/projects` with create and read-by-id endpoints.

#### Scenario: Create project
- **WHEN** a client POSTs a valid Project create request to `/v1/projects`
- **THEN** the API returns `201 Created` with the Project response DTO

#### Scenario: Read project by id
- **WHEN** a client GETs `/v1/projects/{projectId}`
- **THEN** the API returns the Project response DTO or `404` if not found

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
