## MODIFIED Requirements

### Requirement: Service Health Check
The daemon MUST provide a health check endpoint to verify service availability and version.

#### Scenario: Health check success
- **WHEN** a client requests `GET /api/v1/health`
- **THEN** the service returns 200 OK
- **AND** the response contains `ok: true`, `version` string, and `uptimeMs` number

### Requirement: Host Profile Configuration
The daemon MUST allow updating the host profile configuration.

#### Scenario: Update host profile
- **WHEN** a client requests `PUT /api/v1/service/host-profile` with a valid profile payload
- **THEN** the service returns 200 OK
- **AND** the response contains `ok: true`

### Requirement: Command Execution
The daemon MUST provide an endpoint to execute AOS CLI commands.

#### Scenario: Execute command
- **WHEN** a client requests `POST /api/v1/commands` with an `argv` array
- **THEN** the service executes the command
- **AND** returns 200 OK with `ok` boolean and `output` string result

### Requirement: Service lifecycle status
The daemon MUST provide a service lifecycle status endpoint.

#### Scenario: Query current service status
- **WHEN** a client requests `GET /api/v1/service`
- **THEN** the service returns 200 OK
- **AND** the response contains `ok: true`, a `status` value describing the current service state, and a `surfaces` collection describing the available API surfaces

### Requirement: Service lifecycle control
The daemon MUST provide endpoints to control the service lifecycle.

#### Scenario: Start service
- **WHEN** a client requests `POST /api/v1/service/start`
- **THEN** the service returns 200 OK
- **AND** the response contains `ok: true`

#### Scenario: Stop service
- **WHEN** a client requests `POST /api/v1/service/stop`
- **THEN** the service returns 200 OK
- **AND** the response contains `ok: true`

#### Scenario: Restart service
- **WHEN** a client requests `POST /api/v1/service/restart`
- **THEN** the service returns 200 OK
- **AND** the response contains `ok: true`

### Requirement: Daemon API base URL is configurable
The daemon API base URL MUST be configurable via environment/configuration and MUST have a development default.

#### Scenario: Daemon API is reachable at default base URL
- **WHEN** the daemon API is started in development without an explicit base URL override
- **THEN** it listens on `http://localhost:9000`

### Requirement: Daemon API supports CORS for frontend development
The daemon API MUST support CORS for the frontend development origin.

#### Scenario: Browser calls daemon API from frontend dev origin
- **WHEN** the frontend running on a dev origin performs a cross-origin request to the daemon API
- **THEN** the daemon API returns CORS headers allowing the request

## ADDED Requirements

### Requirement: Engine run history
The daemon MUST provide a run history endpoint for engine-level runs.

#### Scenario: List engine runs
- **WHEN** a client requests `GET /api/v1/runs`
- **THEN** the service returns 200 OK
- **AND** the response contains a collection of run summaries

### Requirement: Host log polling
The daemon MUST provide a log retrieval endpoint suitable for polling the current host log stream.

#### Scenario: Retrieve recent logs
- **WHEN** a client requests `GET /api/v1/logs`
- **THEN** the service returns 200 OK
- **AND** the response contains recent log entries

### Requirement: Diagnostics snapshot
The daemon MUST provide a diagnostics snapshot endpoint.

#### Scenario: Retrieve diagnostics snapshot
- **WHEN** a client requests `GET /api/v1/diagnostics`
- **THEN** the service returns 200 OK
- **AND** the response contains diagnostic logs, artifacts, locks, and cache entries

### Requirement: Diagnostics cleanup
The daemon MUST provide endpoints to clear stale locks and cache entries.

#### Scenario: Delete diagnostic locks
- **WHEN** a client requests `DELETE /api/v1/diagnostics/locks`
- **THEN** the service returns 200 OK or 204 No Content
- **AND** the referenced lock entries are removed

#### Scenario: Delete diagnostic cache entries
- **WHEN** a client requests `DELETE /api/v1/diagnostics/cache`
- **THEN** the service returns 200 OK or 204 No Content
- **AND** the referenced cache entries are removed
