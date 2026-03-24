## 1. Documentation and contract alignment

- [x] 1.1 Audit repo docs for daemon/domain URL env var usage (`VITE_DAEMON_URL`, `VITE_DOMAIN_URL`, `DaemonApi__BaseUrl`) and identify any outdated names
- [x] 1.2 Update docs to consistently describe the routing boundary: daemon/engine lifecycle + commands → `nirmata.Windows.Service.Api`; domain/workspace data → `nirmata.Api`
- [x] 1.3 Update docs to clearly distinguish daemon server listen URL (daemon host config) vs frontend daemon base URL (`VITE_DAEMON_URL`)

## 2. Configuration normalization (if needed)

- [x] 2.1 Verify `nirmata.Windows.Service.Api` default listen URL behavior matches spec (`http://localhost:9000` in dev)
- [x] 2.2 If the repo contains older daemon URL env var names, add a compatibility alias (temporary) and document the canonical name
- [x] 2.3 Verify frontend uses `VITE_DAEMON_URL` and `VITE_DOMAIN_URL` as the only base URL sources (no hard-coded fallbacks outside configuration)

## 3. Process shape decision wiring

- [x] 3.1 Document companion-process as the default production shape and capture the in-proc alternative and trade-offs in the appropriate docs/README locations
- [x] 3.2 Define the ownership model for start/stop ordering for service vs daemon API in production (who starts whom, and what happens on failure)

## 4. Optional implementation follow-ups (only if chosen)

- [x] 4.1 If moving to a real Windows Service hosting mode, add `UseWindowsService` and service lifetime wiring to `nirmata.Windows.Service` while keeping console-friendly dev behavior
- [x] 4.2 If selecting in-proc daemon API hosting, implement Kestrel hosting within the service process with graceful shutdown and logging integration
