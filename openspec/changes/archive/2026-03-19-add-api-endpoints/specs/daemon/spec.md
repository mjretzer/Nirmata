## ADDED Requirements

### Requirement: Service Health Check
The daemon MUST provide a health check endpoint to verify service availability and version.

#### Scenario: Health check success
- **WHEN** a client requests `GET /api/v1/health`
- **THEN** the service returns 200 OK
- **AND** the response contains `ok: true`, `version` string, and `latencyMs` number

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
