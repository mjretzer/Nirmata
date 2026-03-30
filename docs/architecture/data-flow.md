# High-Level Data Flow & Component Diagram

Source: High_Level_Data_Flow.pdf (Section 15), Component_Diagram.pdf (Section 13)

---

## System Layers

The system is organized into five distinct planes plus external dependencies.

```
┌─────────────────────────────────────────────────────────────────┐
│  USER                                                           │
│  ├── Direct engine (aos … / freeform)                           │
│  └── Web UI (Gmsd.Web)                                          │
└─────────────────────────────────────────────────────────────────┘
                             │
┌─────────────────────────────────────────────────────────────────┐
│  ENGINE HOSTS (Platform Boundary)                               │
│  ├── Windows Service Host (Gmsd.Windows.Service)                │
│  │     daemon/workers                                           │
│  └── Agent Manager API (Gmsd.Windows.Service.Api)              │
│        Commands / Runs / Service endpoints                      │
└─────────────────────────────────────────────────────────────────┘
                             │
┌─────────────────────────────────────────────────────────────────┐
│  CONTROL PLANE (Governance + Routing)                           │
│  ├── Agent Orchestrator (classify > gate > dispatch > persist)  │
│  ├── State Manager (what to update?)                            │
│  ├── Context Engineer (build deterministic context packets)     │
│  └── Subagent Orchestrator (fresh context per atomic step)      │
└─────────────────────────────────────────────────────────────────┘
         │              │             │              │
┌────────┴─────┐ ┌──────┴──────┐ ┌───┴────┐ ┌──────┴──────────┐
│  BROWNFIELD  │ │  PLANNING   │ │CONTINUITY│ │   EXECUTION     │
│    PLANE     │ │    PLANE    │ │  PLANE   │ │     PLANE       │
│              │ │ (Spec-First │ │          │ │                 │
│  Codebase    │ │  Authoring) │ │ History  │ │ Task Executor   │
│  Mapper      │ │             │ │ Writer   │ │ Atomic Git      │
│  → codebase/ │ │ Interviewer │ │ Pause/   │ │ Committer       │
│              │ │ Roadmapper  │ │ Resume   │ │                 │
│              │ │ Phase       │ │ Progress │ │                 │
│              │ │ Planner     │ │ Reporter │ │                 │
│              │ │ → tasks/    │ │          │ │                 │
│              │ │   */plan.json│ │         │ │                 │
└──────────────┘ └─────────────┘ └──────────┘ └─────────────────┘
                                                        │
                                         ┌──────────────┴──────────────┐
                                         │  VERIFICATION & FIX PLANE   │
                                         │  UAT Verifier               │
                                         │  (verify-work → pass/fail   │
                                         │   + issues)                 │
                                         │  Fix Planner                │
                                         │  (→ fix plans)              │
                                         └─────────────────────────────┘
                             │
┌─────────────────────────────────────────────────────────────────┐
│  AOS WORKSPACE (.aos/)                                          │
│  ├── spec/        (intended truth)                              │
│  ├── state/       (operational truth)                           │
│  ├── evidence/    (provable truth)                              │
│  ├── codebase/    (repo context)                                │
│  ├── context/     (deterministic packs)                         │
│  ├── schemas/     (validation contracts)                        │
│  └── cache/       (non-authoritative ops)                       │
└─────────────────────────────────────────────────────────────────┘
                             │
┌─────────────────────────────────────────────────────────────────┐
│  PRODUCT APPLICATION (non-engine)                               │
│  ├── Product REST API (Gmsd.Api)                                │
│  ├── Product Services Tier (Gmsd.Services)                      │
│  └── Product DB (Gmsd.Data)                                     │
└─────────────────────────────────────────────────────────────────┘
                             │
┌─────────────────────────────────────────────────────────────────┐
│  EXTERNAL DEPENDENCIES                                          │
│  ├── LLM Provider(s)           (OpenAI, Anthropic, Azure, etc.) │
│  ├── MCP Tools Servers         (MCP protocol servers)           │
│  └── Project Files             (source code on disk)            │
└─────────────────────────────────────────────────────────────────┘
```

---

## Data Flow: New Project (Greenfield)

```
User
  └─► aos init
        └─► Workspace created (.aos/)
              └─► New-Project Interviewer
                    └─► .aos/spec/project.json
                          └─► Roadmapper
                                └─► .aos/spec/roadmap.json
                                    .aos/state/state.json
                                    .aos/state/events.ndjson
                                      └─► Phase Planner (PH-0001)
                                            └─► .aos/spec/tasks/TSK-*/plan.json
                                                  └─► Task Executor
                                                        └─► Code changes
                                                            .aos/evidence/runs/RUN-*/
                                                              └─► Atomic Git Committer
                                                                    └─► git commit
                                                                          └─► UAT Verifier
                                                                                ├─► PASS → next phase
                                                                                └─► FAIL → Fix Planner → re-execute
```

---

## Data Flow: Brownfield (Existing Repo)

```
User
  └─► aos codebase scan
        └─► Codebase Mapper Agent
              └─► .aos/codebase/{map,stack,architecture,structure,
                                  conventions,testing,integrations,concerns}.json
                  .aos/codebase/cache/{symbols,file-graph}.json
                    └─► New-Project Interviewer (grounded in codebase map)
                          └─► (continues same as greenfield from here)
```

---

## Data Flow: Pause & Resume

```
User (pause-work)
  └─► Pause/Resume Manager
        └─► .aos/state/handoff.json  (cursor + in-flight task + scope + next cmd)
            .aos/state/events.ndjson  (work.paused event)

User (resume-work) [new session]
  └─► Pause/Resume Manager
        └─► load .aos/state/handoff.json
            confirm matches current spec/roadmap cursor
            rebuild minimal context pack
            └─► resume execution from exact position
```

---

## Component Interaction Summary

| Component | Reads From | Writes To |
|---|---|---|
| Orchestrator | `.aos/spec/**`, `.aos/state/**`, `.aos/evidence/**` | `.aos/state/state.json`, `.aos/state/events.ndjson`, `.aos/evidence/runs/RUN-*/` |
| Interviewer | `.aos/codebase/**` (optional) | `.aos/spec/project.json` |
| Roadmapper | `.aos/spec/project.json` | `.aos/spec/roadmap.json`, `.aos/state/state.json`, `.aos/state/events.ndjson` |
| Phase Planner | `.aos/spec/project.json`, `.aos/spec/roadmap.json`, `.aos/state/state.json` | `.aos/spec/tasks/TSK-*/plan.json`, `.aos/spec/tasks/TSK-*/task.json` |
| Task Executor | `.aos/spec/tasks/TSK-*/plan.json`, `.aos/context/packs/TSK-*.json` | Project source files, `.aos/evidence/runs/RUN-*/` |
| Atomic Git Committer | `.aos/evidence/runs/RUN-*/` | Git commits, `.aos/evidence/task-evidence/TSK-*/latest.json` |
| UAT Verifier | `.aos/spec/tasks/**`, `.aos/spec/roadmap.json` | `.aos/spec/issues/ISS-*.json`, `.aos/spec/uat/UAT-*.json`, `.aos/state/state.json` |
| Fix Planner | `.aos/spec/issues/ISS-*.json`, `.aos/spec/uat/UAT-*.json` | `.aos/spec/tasks/TSK-*/plan.json` (fix tasks) |
| Codebase Mapper | Repo source files | `.aos/codebase/**`, `.aos/codebase/cache/**` |
| Context Engineer | `.aos/spec/**`, `.aos/state/**`, `.aos/codebase/**` | `.aos/context/packs/<TSK\|PH>-*.json` |
| State Manager | `.aos/state/state.json` | `.aos/state/state.json`, `.aos/state/events.ndjson` |
| History Writer | `.aos/evidence/runs/RUN-*/`, `.aos/state/state.json` | Narrative summary, `.aos/state/state.json` |
| Pause/Resume Manager | `.aos/state/state.json`, `.aos/evidence/runs/**` | `.aos/state/handoff.json`, `.aos/state/events.ndjson` |

---

## AOS Workspace Truth Layers (Summary)

| Layer | Directory | What It Holds | Who Owns It |
|---|---|---|---|
| Intended truth | `.aos/spec/` | The plan: goals, roadmap, tasks, what "done" means | Planning agents |
| Operational truth | `.aos/state/` | Current cursor, decisions, blockers, event trail | State Manager |
| Provable truth | `.aos/evidence/` | Actual execution records, logs, artifacts, commits | Task Executor + Git Committer |
| Repo intelligence | `.aos/codebase/` | How the repo works: stack, conventions, structure | Codebase Mapper |
| Context packs | `.aos/context/` | Deterministic bounded bundles for agent runs | Context Engineer |
| Validation contracts | `.aos/schemas/` | JSON schemas for every artifact type | CLI / Gmsd.Aos |
| Ops support | `.aos/cache/` | Locks, temp files (disposable) | Maintenance |

---

## Frontend API Routing

The React frontend (`nirmata.frontend/`) talks to **two distinct backend processes**. The routing rule is strict and must not be blurred:

| Traffic | Backend | Env var (frontend) | Server-side config |
|---|---|---|---|
| Daemon/engine lifecycle + commands | `nirmata.Windows.Service.Api` | `VITE_DAEMON_URL` | `DaemonApi__BaseUrl` |
| Domain/workspace data (workspaces, spec, tasks, runs) | `nirmata.Api` | `VITE_DOMAIN_URL` | _(standard ASP.NET Core URL config)_ |

### Rules

- **Daemon traffic** (`VITE_DAEMON_URL`, default `https://localhost:9000`): health checks, service start/stop/restart, host-profile updates, AOS command execution — everything that controls or observes the engine host process. Always targets `nirmata.Windows.Service.Api`.
- **Domain traffic** (`VITE_DOMAIN_URL`): workspace CRUD, spec/state reads, task data, run history — all persistent domain entities. Always targets `nirmata.Api`.
- **No hard-coded base URLs** in component or hook code. All base URLs flow from `VITE_DAEMON_URL` / `VITE_DOMAIN_URL` (see `nirmata.frontend/src/app/api/routing.ts`).

### Env var naming

| Name | Scope | Kind | Purpose |
|---|---|---|---|
| `VITE_DAEMON_URL` | Frontend (`.env.local`) | **Client config** | Base URL the browser uses for outbound calls to the daemon API |
| `VITE_DOMAIN_URL` | Frontend (`.env.local`) | **Client config** | Base URL the browser uses for outbound calls to the domain API |
| `DaemonApi__BaseUrl` | Daemon server (`nirmata.Windows.Service.Api`) | **Host config** | URL the daemon process binds and listens on |

### Daemon server listen URL vs frontend daemon base URL

These are two distinct, independently owned configuration knobs that happen to point at the same address by default:

| Config | Owner | Responsibility |
|---|---|---|
| `DaemonApi__BaseUrl` | Daemon server process | **Host config** — controls where the server binds (inbound). Set in `appsettings.json` / environment of `nirmata.Windows.Service.Api`. |
| `VITE_DAEMON_URL` | React frontend | **Client config** — controls where the browser sends requests (outbound). Set in `.env.local` of `nirmata.frontend/`. |

**Rules:**
- `DaemonApi__BaseUrl` is owned by the server host process — it is not a frontend concern.
- `VITE_DAEMON_URL` is owned by the frontend client — it is not a server concern.
- Both default to `https://localhost:9000` in development, but are configured independently.
- When overriding either value, **both must be updated** to a consistent address or the frontend will fail to reach the daemon API.
- Never hard-code either value in component or hook code — always read from the configured source.

---

## Process Shape & Start/Stop Ordering

### Default: companion-process

The recommended production shape is two separate processes:

| Process | Project | Role |
|---|---|---|
| Engine host | `nirmata.Windows.Service` | Long-lived worker/engine; runs as a Windows Service |
| Daemon API | `nirmata.Windows.Service.Api` | HTTP API for daemon control, health, and engine commands |

Both processes are console-hostable, keeping local dev and production structurally identical.

### Start/stop ordering

**Start order:** Engine host → Daemon API

The daemon API exposes engine status. If it starts before the engine is ready, health polls return a disconnected state until the engine comes up — this is safe and handled gracefully.

**Stop order:** Daemon API → Engine host

Stopping the API first prevents new commands from being accepted while the engine is shutting down.

**Failure recovery:**

| Failure | Effect | Recovery |
|---|---|---|
| Daemon API crashes | Engine keeps running | Restart `nirmata.Windows.Service.Api` only |
| Engine host crashes | Daemon API stays up; reports engine disconnected | Restart `nirmata.Windows.Service` only |

Neither failure requires restarting the other process.

### Alternative: in-proc daemon API

The daemon HTTP API can instead be hosted inside `nirmata.Windows.Service` (Kestrel in-proc). Trade-offs:

| Aspect | Companion-process (default) | In-proc |
|---|---|---|
| Deployment | Two processes | Single process |
| Failure isolation | Independent | API crash kills engine |
| Debug experience | Each process attaches independently | More complex under Service Control Manager |
| Shutdown | Ordered stop sequence | Single shutdown path |

The in-proc option is explicitly supported as an alternative but is not the default. See `src/nirmata.Windows.Service.Api/README.md` for full detail.
