## Context

Nirmata currently exposes multiple backend entrypoints:

- `nirmata.Api`: domain/workspace data API (console-friendly, EF Core SQLite, health checks).
- `nirmata.Windows.Service`: worker/engine host implemented as a generic host + background loop; today it runs as a console process and does not yet opt into Windows Service hosting APIs.
- `nirmata.Windows.Service.Api`: daemon-facing HTTP API intended to expose service health, host profile configuration, and engine/command execution.

The frontend uses two base URLs:

- `VITE_DAEMON_URL` for daemon/engine/service lifecycle calls (default `http://localhost:9000`).
- `VITE_DOMAIN_URL` for domain/workspace data.

There is a documented intent that daemon/host/service lifecycle and engine commands route to `nirmata.Windows.Service.Api`, while workspace/domain data routes to `nirmata.Api`. However, production shape (single vs multiple processes) and the precise configuration contract between daemon listen URL vs frontend base URL have been inconsistently documented.

Constraints:

- Development should remain debuggable by default (F5/attach to console processes).
- Production should support a Windows Service for long-lived worker execution.
- The frontend routing rule must remain strict and obvious to contributors.

## Goals / Non-Goals

**Goals:**

- Establish a clear default production shape: Windows Service runs the long-lived worker/engine, while daemon HTTP API runs as a companion process by default.
- Document the alternative (in-proc daemon API hosted inside the service process) as an explicit option, with trade-offs.
- Make configuration naming and responsibility clear:
  - daemon server listen URL (hosted by `nirmata.Windows.Service.Api`) vs
  - frontend daemon base URL (`VITE_DAEMON_URL`).
- Reinforce the routing boundary between daemon API and domain API.

**Non-Goals:**

- Designing cross-machine clustering, remote daemon access, or non-localhost security hardening.
- Implementing full Windows Service installation/packaging steps.
- Moving existing endpoints between APIs unless required by the routing boundary.

## Decisions

### Decision: Default to companion-process production shape

**Choice:** Windows Service hosts the long-lived engine; daemon HTTP API runs as a separate process (potentially supervised/started by the service, but still a distinct executable).

**Rationale:**

- Keeps development and production as similar as possible (both are console-hostable processes).
- Failure isolation: API crashes need not take down the engine and vice versa.
- Avoids mixing Kestrel hosting concerns and Windows Service lifecycle concerns in a single host until required.

**Alternative considered:** In-proc daemon API hosted inside the Windows Service process.

- Benefits: one process to deploy/install; simple localhost IPC.
- Costs: harder debugging as a true service; a fatal API failure can kill the engine; more complex shutdown/logging integration with the Service Control Manager.

### Decision: Treat routing as an explicit contract

**Choice:** The frontend and developer documentation treat the daemon API vs domain API split as a hard boundary.

- `nirmata.Windows.Service.Api` owns:
  - service/host/engine lifecycle
  - command execution surface
  - daemon health
- `nirmata.Api` owns:
  - workspace/project/spec/run/issue domain data

**Rationale:**

- Prevents endpoint creep and “just add it to whichever API is running” behavior.
- Keeps domain models separate from service-host concerns.

### Decision: Configuration naming is split by responsibility

**Choice:**

- Daemon server listen URL is configured on the daemon API host process (e.g., `DaemonApi__BaseUrl`, default `http://localhost:9000`).
- Frontend daemon base URL remains `VITE_DAEMON_URL` (default `http://localhost:9000`).

**Rationale:**

- Server listen URL is host-level configuration; frontend base URL is client configuration.
- Similar defaults keep local dev friction low while still clarifying which side owns which config.

## Risks / Trade-offs

- [Two processes to deploy/configure] → Mitigation: document ownership and ordering, and define defaults that work out-of-box for local dev.
- [Configuration drift / mismatched env var names] → Mitigation: define a single authoritative naming scheme and update docs to match; optionally support a compatibility alias for any older names that exist in the repo.
- [Endpoint placement confusion] → Mitigation: add a spec that explicitly defines routing responsibility and reference it from docs and contributing guidance.
- [Future desire for single-process deployment] → Mitigation: keep the in-proc option documented; design the daemon API and engine host boundaries to allow later consolidation if needed.
