## 1. Daemon API surface + configuration

- [x] 1.1 Confirm `nirmata.Windows.Service.Api` runs as a console-hosted ASP.NET Core app in development
- [x] 1.2 Add env/config support for daemon API base URL (default `http://localhost:9000`)
- [x] 1.3 Configure CORS in `nirmata.Windows.Service.Api` to allow the frontend dev origin
- [x] 1.4 Document the daemon API base URL configuration (env vars, defaults) for local development

## 2. Daemon API endpoints (minimal lifecycle surface)

- [x] 2.1 Implement `GET /api/v1/health` response shape per `daemon` spec (ok/version/latencyMs)
- [x] 2.2 Implement `PUT /api/v1/service/host-profile` endpoint per `daemon` spec
- [x] 2.3 Implement `GET /api/v1/service` endpoint (service status payload)
- [x] 2.4 Implement `POST /api/v1/service/start` endpoint (may be stubbed but returns stable shape)
- [x] 2.5 Implement `POST /api/v1/service/stop` endpoint (may be stubbed but returns stable shape)
- [x] 2.6 Implement `POST /api/v1/service/restart` endpoint (may be stubbed but returns stable shape)

## 3. Routing map + frontend wiring (API vs daemon)

- [x] 3.1 Identify all frontend features/hooks that must route to the daemon API (health poll, host/service lifecycle, engine connection)
- [x] 3.2 Identify all frontend features/hooks that must route to the domain data API (projects/workspaces/phases/tasks/runs/issues)
- [x] 3.3 Create/update a small routing map reference in frontend code (single source of truth for base URLs)
- [x] 3.4 Replace remaining daemon-side MSW mocks by wiring hooks to the daemon endpoints
- [x] 3.5 Ensure the frontend uses the env-driven daemon base URL (defaulting to `http://localhost:9000`)

## 4. Validation

- [x] 4.1 Validate CORS by calling daemon endpoints from the frontend dev server
- [x] 4.2 Smoke test service lifecycle endpoints via curl/Swagger (status/start/stop/restart)
- [x] 4.3 Verify the routing rule holds (no daemon/host features calling `nirmata.Api`, no domain data features calling daemon API)
