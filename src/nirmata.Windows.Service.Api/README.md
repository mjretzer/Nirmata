# nirmata.Windows.Service.Api — Local Development

The daemon/engine API. Handles service lifecycle (start/stop/restart), host-profile, and health endpoints.
It is a **separate surface** from `nirmata.Api` (which handles domain data).

---

## Running locally

```bash
cd src/nirmata.Windows.Service.Api
dotnet run
```

The API starts on `https://localhost:9000` by default. Swagger UI is available at `https://localhost:9000/swagger`.

When you run the frontend over HTTPS for GitHub OAuth, allow the secure browser origin `https://localhost:8443` in the daemon CORS settings.

---

## Configuration

### Daemon API base URL

| Key | Env var | Default | Purpose |
|---|---|---|---|
| `DaemonApi:BaseUrl` | `DaemonApi__BaseUrl` | `https://localhost:9000` | Listening URL for this process |

Override via environment variable:

```bash
DaemonApi__BaseUrl=https://localhost:9001 dotnet run
```

Or in `appsettings.Development.json`:

```json
{
  "DaemonApi": {
    "BaseUrl": "https://localhost:9001"
  }
}
```

### CORS allowed origins

| Key | Env var | Default (dev) | Purpose |
|---|---|---|---|
| `Cors:AllowedOrigins` | `Cors__AllowedOrigins__0`, `__1`, … | `https://localhost:8443` | Origins allowed to call the daemon API |

The secure frontend origin (`https://localhost:8443`) is pre-configured in `appsettings.Development.json`.
Production deployments must set `Cors:AllowedOrigins` explicitly (the base `appsettings.json` ships with an empty list).

Override via environment variable (array syntax):

```bash
Cors__AllowedOrigins__0=https://localhost:8443 dotnet run
```

---

## Frontend wiring

The frontend reads the daemon base URL from `VITE_DAEMON_URL` (defaults to `https://localhost:9000`).
Set it in `nirmata.frontend/.env.local` if you run the daemon on a non-default port:

```
VITE_DAEMON_URL=https://localhost:9001
```

---

## Production Shape

### Default: companion-process

The recommended production shape runs two separate processes:

| Process | Project | Role |
|---|---|---|
| Engine host | `nirmata.Windows.Service` | Long-lived worker/engine; runs as a Windows Service |
| Daemon API | `nirmata.Windows.Service.Api` | HTTPS API; exposes service lifecycle, host profile, and engine commands |

**Why separate processes:**
- Failure isolation — an API crash does not kill the engine, and vice versa.
- Each process can be debugged, restarted, or updated independently.
- The console-mode dev workflow and Windows Service production shape are structurally identical; no special-casing needed.

### Start/stop ordering

**Starting:**
1. Start `nirmata.Windows.Service` (engine host) first.
2. Start `nirmata.Windows.Service.Api` (daemon API) second.

The daemon API exposes engine status. If the API starts before the engine is ready, health polls return a disconnected state until the engine comes up — this is expected and handled gracefully by the frontend.

**Stopping:**
1. Stop `nirmata.Windows.Service.Api` first — prevents new commands from being dispatched while the engine is winding down.
2. Stop `nirmata.Windows.Service` second.

**On daemon API failure:**
The engine host continues running. Restart `nirmata.Windows.Service.Api` independently. The engine does not need to be restarted.

**On engine host failure:**
The daemon API remains available but reports the engine as disconnected. Restart `nirmata.Windows.Service` independently. The daemon API does not need to be restarted.

### Alternative: in-proc daemon API

An alternative is to host the daemon HTTP API inside the same process as the Windows Service (Kestrel inside `nirmata.Windows.Service`).

| Aspect | Companion-process (default) | In-proc |
|---|---|---|
| Deployment | Two processes to start/install | Single process |
| Failure isolation | API crash does not affect engine | API crash kills engine |
| Debug experience | Each process attaches independently | More complex under Service Control Manager |
| Shutdown coordination | Ordered stop sequence (see above) | Single `IHostedService` shutdown |
| Logging | Independent log streams | Shared log stream |

The in-proc option is not the current default but is explicitly supported as an alternative deployment shape.
