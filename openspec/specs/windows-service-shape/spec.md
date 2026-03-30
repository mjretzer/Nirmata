# Windows Service Shape Specification
 
## Purpose
Define the supported production and development process shapes for Windows Service hosting, including routing boundaries and configuration responsibilities.
 
## Requirements

### Requirement: Companion-process is the default production shape
The system MUST support a production shape where the Windows Service hosts the long-lived engine/worker and the daemon HTTP API runs as a separate companion process.

#### Scenario: Service and daemon API run as separate processes
- **WHEN** the system is deployed in production using the default shape
- **THEN** the engine/worker runs under the Windows Service host process
- **AND** the daemon HTTP API runs in a separate process hosting Kestrel

### Requirement: In-proc daemon API hosting is an explicit alternative
The system MUST allow an alternative deployment shape where the daemon HTTP API is hosted in-proc with the Windows Service host process.

#### Scenario: Daemon API runs in the same process as the service
- **WHEN** the system is configured for in-proc hosting
- **THEN** the daemon HTTP API is hosted by the same process as the Windows Service host

### Requirement: Frontend routing boundary is strict
The system MUST maintain a strict routing boundary between daemon/host/engine endpoints and domain/workspace endpoints.

#### Scenario: Frontend calls daemon endpoints
- **WHEN** the frontend performs requests for service lifecycle, host configuration, health, or engine command execution
- **THEN** the request base URL is the configured daemon base URL
- **AND** the request targets endpoints owned by the daemon API surface

#### Scenario: Frontend calls domain endpoints
- **WHEN** the frontend performs requests for workspace/project/spec/run/issue domain data
- **THEN** the request base URL is the configured domain API base URL
- **AND** the request targets endpoints owned by the domain API surface

### Requirement: Daemon server listen URL is configurable with a development default
The daemon API host MUST support configuration of its listen URL via environment/configuration and MUST provide a development default.

#### Scenario: Daemon API uses default listen URL
- **WHEN** the daemon API is started in development without an explicit listen URL override
- **THEN** it listens on `https://localhost:9000`

### Requirement: Daemon server listen URL and frontend daemon base URL responsibilities are distinct
The system MUST treat the daemon server listen URL as host configuration and the frontend daemon base URL as client configuration.

#### Scenario: Distinct configuration variables exist
- **WHEN** the daemon API process is configured
- **THEN** it uses a host-owned configuration key for its listen URL
- **AND** the frontend uses `VITE_DAEMON_URL` as its daemon base URL

### Requirement: Debuggability is preserved in development
The system MUST support running the engine host, daemon API, and domain API as console-hosted processes for local debugging.

#### Scenario: Debuggable dev workflow
- **WHEN** a developer runs `nirmata.Api`, `nirmata.Windows.Service.Api`, and `nirmata.Windows.Service` locally
- **THEN** each process can be started as a normal console app and debugged via F5/attach
