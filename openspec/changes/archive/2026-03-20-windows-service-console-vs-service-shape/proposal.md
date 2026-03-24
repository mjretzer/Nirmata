## Why

Nirmata currently has multiple backend entrypoints (domain API, daemon API, and a worker host), but the intended production shape and frontend routing rules are only partially documented. This creates confusion in development, increases the chance of endpoints landing in the wrong surface, and risks configuration drift (e.g., mismatched env var names).

## What Changes

- Define and document a single recommended production shape for the Windows-hosted engine + daemon API (companion process by default), while explicitly capturing the alternative in-proc option.
- Make the frontend routing rule explicit and enforceable:
  - daemon/host/service lifecycle + engine commands route to `nirmata.Windows.Service.Api`
  - workspace/domain data routes to `nirmata.Api`
- Standardize and document configuration variables for daemon API listen URL vs frontend daemon base URL, including any required compatibility aliases.
- Add a spec describing the Windows service / console-hosted debug experience expectations so the dev workflow mirrors production reliably.

## Capabilities

### New Capabilities
- `windows-service-shape`: Define the expected process model (companion-process default vs in-proc option), configuration keys/env vars, and the strict routing boundary between daemon API and domain API.

### Modified Capabilities

## Impact

- Backend entrypoints:
  - `src/nirmata.Api` (domain/workspace data)
  - `src/nirmata.Windows.Service.Api` (daemon/engine control surface)
  - `src/nirmata.Windows.Service` (engine host)
- Frontend configuration:
  - `VITE_DAEMON_URL` (daemon API base URL)
  - `VITE_DOMAIN_URL` (domain API base URL)
- Documentation updates where older or inconsistent environment variable names are referenced.
