# Solution Structure (Gmsd.sln)

Source: gmsd_sln.pdf (Section 14)

---

## Top-Level Solution Layout

```
Gmsd.sln
├── src/
│   ├── Gmsd.Api/
│   ├── Gmsd.Common/
│   ├── Gmsd.Data/
│   ├── Gmsd.Data.Dto/
│   ├── Gmsd.Windows.Service.Api/
│   ├── Gmsd.Windows.Service/
│   ├── Gmsd.Agents/
│   ├── Gmsd.Aos/
│   ├── Gmsd.Services/
│   └── Gmsd.Web/
├── nirmata.frontend/              ← React / Vite / TypeScript frontend (shadcn/ui)
├── tests/
├── build/
└── docs/
```

---

## Project Details

### `Gmsd.Api/`
```
Gmsd.Api.csproj
Program.cs
appsettings.json
appsettings.Development.json
Properties/
  launchSettings.json
Controllers/
  v1/
    HealthController.cs
    GmsdController.cs
```
**Purpose:** Product REST API. The external-facing HTTP API surface for the product application tier.

---

### `Gmsd.Common/`
```
Gmsd.Common.csproj
Constants/
Extensions/
Helpers/
Exceptions/
Results/
Configurations/
```
**Purpose:** Shared utilities, constants, extension methods, result types, and exception definitions used across all projects.

---

### `Gmsd.Data/`
```
Gmsd.Data.csproj
Entities/
Context/
Migrations/
Mapping/
```
**Purpose:** Product database layer — EF Core entities, DbContext, migrations, and mapping.

---

### `Gmsd.Data.Dto/`
```
Gmsd.Data.Dto.csproj
Models/
Requests/
Validators/
```
**Purpose:** Data transfer objects, request/response models, and FluentValidation validators for the API boundary.

---

### `Gmsd.Windows.Service.Api/`
```
Gmsd.Windows.Service.Api.csproj
Program.cs
appsettings.json
appsettings.Development.json
Controllers/
  v1/
    HealthController.cs
    ServiceController.cs
    RunsController.cs
    CommandsController.cs
```
**Purpose:** Agent Manager API — the HTTP API that host processes use to start, monitor, and control agent runs. Exposes high-level runtime contracts (commands, runs, service health) while hiding internal engine details.

---

### `Gmsd.Windows.Service/`
```
Gmsd.Windows.Service.csproj
Program.cs
appsettings.json
Configuration/
Models/
Workers/
Execution/
Persistence/
Observability/
```
**Purpose:** Windows Service Host (`Gmsd.Windows.Service`) — the daemon/worker process that runs the agent engine as a background Windows Service.

---

### `Gmsd.Agents/`

The core agent/orchestration project. Full tree:

```
Gmsd.Agents.csproj
appsettings.json
appsettings.Development.json

Engine/
  Contracts/           # Stable interfaces: how engine components interact
  Models/              # Core models shared across the engine
  Artifacts/           # Canonical read/write access to workspace files
  Paths/               # ID-to-file mapping (TSK/PH/MS/UAT/RUN → paths)
  Execution/           # Run-time machinery: workflow dispatch, control flow
  State/               # Run-lifecycle record (status, cursor, events, checkpoints)
  Validation/          # Gate: proves outputs are structurally/semantically correct
  Steps/               # Atomic composable units of work (LLM call, tool call, validate, persist)

Workflows/
  _Shared/             # Common workflow utilities and small internal models
  ControlPlane/
    Orchestrator/
    SubagentRuns/
    Continuity/
  Planning/
    NewProjectInterviewer/
    Roadmapper/
    PhasePlanner/
  Brownfield/
    CodebaseMapper/
  Execution/
    TaskExecutor/
    AtomicGitCommitter/
  Verification/
    UatVerifier/
    FixPlanner/

Llm/
  Contracts/           # Stable LLM interfaces (ITool, message shapes, descriptors)
  Adapters/
    OpenAi/
    Anthropic/
    Ollama/
    AzureOpenAi/

Tools/
  Contracts/           # ITool interface, tool descriptors, request/result shapes
  Registry/            # Tool catalog: registers, resolves, and invokes tools by id/name
  Mcp/                 # MCP adapter: turns MCP servers into ITool implementations
  Aos/                 # Integration boundary to Gmsd.Aos for artifact ops + validation
  Files/               # Filesystem tools: read/write/list workspace content
  Process/             # Process execution tools: run commands, capture stdout/stderr
  Git/                 # Source control tools: status/stage/commit workflows

Prompts/
  Templates/
    system.core.md
    workflows/         # Per-workflow prompt templates

Composition/           # DI wiring: AddGmsdAgents(...) composition root
Public/                # Stable API surface for hosts (Windows Service / API)
```

---

### `Gmsd.Aos/`

The AOS workspace library. Full tree:

```
Gmsd.Aos.csproj

Public/
  Catalogs/            # Schema ids, command ids — authoritative registries
  Services/            # Narrow interfaces mirroring AOS command groups

Engine/
  Workspace/           # Creates/opens workspace; composes all services
  Paths/               # Canonical ID-to-file mapping for all layers
  Serialization/       # Central JSON read/write policy (formatting, ordering, safety)
  Schemas/             # Loads and serves embedded JSON schemas by id
  Validation/          # Schema validation + cross-file integrity rules
  Spec/                # CRUD + indexing for .aos/spec/* (intended-truth artifacts)
  State/               # Manages .aos/state/* (state.json + events.ndjson)
  Evidence/            # Manages .aos/evidence/* (runs/logs/artifacts/task-evidence)
  Checkpoints/         # Snapshots/restores operational state
  Codebase/            # Generates repo intelligence under .aos/codebase/*
  Context/             # Builds deterministic context packs under .aos/context/*
  Maintenance/         # Cache and lock hygiene under .aos/cache/*
  ImportExport/        # Moves workspace slices in/out of repos

Resources/
  Schemas/             # Embedded, version-controlled JSON schema files
  Templates/           # Deterministic context pack templates

Composition/           # DI wiring for Gmsd.Aos (usable without container)
```

---

### `Gmsd.Services/`
```
Gmsd.Services.csproj
Interfaces/
Implementations/
```
**Purpose:** Product Services Tier — business logic interfaces and implementations for the product application (non-engine layer).

---

### `Gmsd.Web/`
```
Gmsd.Web.csproj
Program.cs
appsettings.json
wwwroot/
  css/
    site.css
  js/
    site.js
assets/
Views/
  Shared/
    _Layout.cshtml
    _ValidationScriptsPartial.cshtml
Pages/
  Index.cshtml + Index.cshtml.cs
  Error.cshtml + Error.cshtml.cs
```
**Purpose:** Razor Pages web frontend for the product application.

### `nirmata.frontend/`
```
package.json
vite.config.ts
tsconfig.json
index.html
src/
  main.tsx
  App.tsx
  components/        # shadcn/ui + custom components
  pages/             # Route-level page components
  hooks/             # Custom React hooks
  lib/               # Utilities, API clients
  types/             # TypeScript type definitions
```
**Purpose:** React / Vite / TypeScript frontend for the Nirmata orchestration engine UI. Uses shadcn/ui component library. This is the primary operator-facing interface for running commands, viewing orchestration state, monitoring runs, and interacting with the agent pipeline.

---

## Module Responsibility Summary

| Module | Project | Responsibility |
|---|---|---|
| `Engine/Workspace` | `Gmsd.Aos` | Workspace lifecycle; creates/opens workspace and composes all services with shared config |
| `Engine/Paths` | `Gmsd.Aos` | Canonical ID→file mapping so every layer writes to the same locations predictably |
| `Engine/Serialization` | `Gmsd.Aos` | Central JSON read/write policy (formatting, ordering, safety) to prevent drift and partial writes |
| `Engine/Schemas` | `Gmsd.Aos` | Loads and serves embedded JSON schemas by id for consistent, versioned validation |
| `Engine/Validation` | `Gmsd.Aos` | Schema + cross-file integrity rules; fail-fast with normalized report |
| `Engine/Spec` | `Gmsd.Aos` | CRUD + indexing for `.aos/spec/*` (intended truth) |
| `Engine/State` | `Gmsd.Aos` | Manages `.aos/state/*` (state.json + events.ndjson) for resumability and traceable transitions |
| `Engine/Evidence` | `Gmsd.Aos` | Manages `.aos/evidence/*` (runs/logs/artifacts/task-evidence) for auditable, reproducible records |
| `Engine/Checkpoints` | `Gmsd.Aos` | Snapshot/restore operational state for safe rollbacks |
| `Engine/Codebase` | `Gmsd.Aos` | Generates repo intelligence under `.aos/codebase/*` to reduce repeated discovery |
| `Engine/Context` | `Gmsd.Aos` | Builds deterministic, size-bounded context packs under `.aos/context/*` |
| `Engine/Maintenance` | `Gmsd.Aos` | Cache/lock hygiene under `.aos/cache/*` for safe, concurrent operations |
| `Engine/ImportExport` | `Gmsd.Aos` | Moves workspace slices in/out of repos for portability, backup, and templating |
| `Engine/Contracts` | `Gmsd.Agents` | Internal interfaces and core models that define how engine components interact |
| `Engine/Models` | `Gmsd.Agents` | Core models shared across the engine |
| `Engine/Artifacts` | `Gmsd.Agents` | Canonical read/write access to workspace spec/state/evidence/codebase files for workflows |
| `Engine/Paths` | `Gmsd.Agents` | ID-to-file mapping (TSK/PH/MS/UAT/RUN → paths) for the agent layer |
| `Engine/Execution` | `Gmsd.Agents` | Run-time machinery: takes a workflow request and executes it end-to-end (dispatch, control flow, result) |
| `Engine/State` | `Gmsd.Agents` | Authoritative run-lifecycle record (status, cursor, events, checkpoints) used for continuity and auditability |
| `Engine/Validation` | `Gmsd.Agents` | Gate that proves outputs are structurally and semantically correct; fails fast and emits normalized report |
| `Engine/Steps` | `Gmsd.Agents` | Atomic composable units of work (LLM call, tool call, validate, persist) assembled by workflows |
| `Llm/Contracts` | `Gmsd.Agents` | Stable LLM interfaces independent of any vendor implementation |
| `Llm/Adapters` | `Gmsd.Agents` | Vendor-specific LLM implementations: OpenAI, Anthropic, Ollama, Azure OpenAI |
| `Prompts/Templates` | `Gmsd.Agents` | Managed prompt assets and templating logic kept separate from engine code |
| `Tools/Contracts` | `Gmsd.Agents` | Stable tool interface (ITool, descriptors, request/result shapes) |
| `Tools/Registry` | `Gmsd.Agents` | Tool catalog: registers, resolves, and invokes tools by id/name at runtime |
| `Tools/Mcp` | `Gmsd.Agents` | MCP adapter: turns MCP servers/endpoints into first-class ITool implementations |
| `Tools/Aos` | `Gmsd.Agents` | Integration boundary to Gmsd.Aos for artifact ops + schema/state validation |
| `Tools/Files` | `Gmsd.Agents` | Filesystem tools for controlled, auditable workspace read/write/list |
| `Tools/Process` | `Gmsd.Agents` | Process execution tools: run commands (build/test/format), capture stdout/stderr as evidence |
| `Tools/Git` | `Gmsd.Agents` | Source control tools: status/stage/commit for atomic, traceable changes |
| `Composition` | `Gmsd.Agents` | DI root: `AddGmsdAgents(...)` wires engine, workflows, LLM adapters, and tools for host processes |
| `Public` | `Gmsd.Agents` | Stable API surface for hosts (Windows Service / API); hides all internal implementation details |
| `Public/Catalogs` | `Gmsd.Aos` | Authoritative registries: schema ids, command ids |
| `Public/Services` | `Gmsd.Aos` | Narrow interfaces mirroring AOS command groups — the only surface other projects compile against |
| `Resources/Schemas` | `Gmsd.Aos` | Embedded, version-controlled JSON schema files that define structural validity for every artifact type |
| `Resources/Templates` | `Gmsd.Aos` | Deterministic templates for context packs |
| `Composition` | `Gmsd.Aos` | DI wiring for Gmsd.Aos; usable without a container |
