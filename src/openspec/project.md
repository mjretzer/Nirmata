# nirmata Platform — Project Overview & Separation of Concerns

## 1) What this solution is
nirmata is a multi-project .NET solution with **two major domains**:

1) **Product Application (non-engine)**  
   The user-facing app surface: Web UI + Product REST API + Product Services + Product DB.

2) **Agent Orchestration Engine (AOS / “engine”)**  
   The orchestration/runtime responsible for spec-first planning, deterministic execution, validation, evidence capture, and resumability via the `.aos/*` workspace.

This document exists to keep those domains cleanly separated so we don’t mix **product features** with **engine mechanics**.

---

## 2) Separation of concerns (the rule-of-thumb)
### Product Application concerns
Owns:
- Business/domain features (Projects/Steps, product-facing endpoints, UI)
- Persistence of product data in SQLite via EF Core
- DTO mapping and validation for product APIs
- UI rendering (Razor Pages)

Does **not** own:
- Workflow state machine (plan → execute → verify → fix)
- Run/evidence capture, context packs, codebase intelligence
- Task/phase/milestone lifecycle (as engine artifacts)

### Engine (AOS) concerns
Owns:
- AOS workspace file contracts (`.aos/spec`, `.aos/state`, `.aos/evidence`, `.aos/context`, `.aos/codebase`, `.aos/cache`)
- Deterministic JSON IO + formatting/ordering safety rules (no partial/invalid writes)
- Embedded schemas + validators (schema validation + cross-file invariants)
- Tool contracts for non-LLM tools (MCP tools, filesystem/process/git tools)
- Evidence capture interfaces (for tool calls, LLM calls via primitive types)

Does **not** own:
- Workflow orchestration logic (plan → execute → verify → fix control loop)
- Control-plane routing & gating (classify → gate → dispatch)
- Product REST API surface, product DTOs, product EF Core models/migrations
- Product UI concerns
- Product business rules

---

## 3) Solution project map (ownership)
### Product Application (non-engine)
- **nirmata.Web**
  - Razor Pages UI + static assets
  - Talks to Product Services (not to internal engine workflows)

- **nirmata.Api**
  - Product REST API surface (controllers, OpenAPI)
  - Uses Product Services + DTOs

- **nirmata.Services**
  - Product business logic & application services
  - Uses Data + DTOs
  - No AOS workflow logic here

- **nirmata.Data**
  - EF Core DbContext, domain entities, migrations
  - SQLite + lazy-loading proxies
  - Owns cascade rules (Project → Steps)

- **nirmata.Data.Dto**
  - DTOs + request/response models + validators

### Engine (AOS / infrastructure)
- **nirmata.Aos**
  - Core engine library: workspace contracts, schemas, validation, serialization, pathing, artifact catalogs
  - Evidence capture infrastructure (LLM-agnostic, uses primitive types)
  - Tool contracts and execution framework (ITool, registry, invocation)
  - Command routing infrastructure (dispatch only, no workflow logic)

### Plane (Agent orchestration layer)
- **nirmata.Agents**
  - Workflow orchestration: control loop (classify → gate → dispatch → validate → persist → next)
  - Planning & execution workflows + verification/fix loop
  - Run lifecycle + resumability (pause/resume)
  - Context-pack assembly and delegation patterns
  - LLM orchestration: provider abstractions (ILlmProvider, message types, tool definitions, adapters)
  - Prompt template loading and management
  - Composes workflows against AOS contracts/tools

### Hosts (runtime)
- **nirmata.Windows.Service**
  - Host process (daemon/workers) for running engine workflows

- **nirmata.Windows.Service.Api**
  - “Agent Manager API” surface for controlling/observing runs (commands/runs/health/service)

### Shared
- **nirmata.Common**
  - Cross-cutting helpers/constants/exceptions shared across product + engine
  - Must remain low-level and dependency-light

---

## 4) Dependency direction (compile-time rules)
**Goal:** keep the engine swappable and the product app clean.

### Allowed references (high level)
- `nirmata.Data` → `nirmata.Common`
- `nirmata.Data.Dto` → `nirmata.Common`
- `nirmata.Services` → `nirmata.Data`, `nirmata.Data.Dto`, `nirmata.Common`
- `nirmata.Api` → `nirmata.Services`, `nirmata.Data.Dto`, `nirmata.Common`
- `nirmata.Web` → `nirmata.Services`, `nirmata.Data.Dto`, `nirmata.Common`

- `nirmata.Aos` → `nirmata.Common` (and internal engine dependencies only)
- `nirmata.Agents` → `nirmata.Aos`, `nirmata.Common` (must NOT reference Product projects)
- Hosts (`nirmata.Windows.Service*`) → `nirmata.Agents`, `nirmata.Aos`, `nirmata.Common`

### Not allowed (hard boundaries)
- Engine projects must not reference **Product Application** projects:
  - `nirmata.Aos` / `nirmata.Agents` / hosts **must not** reference `nirmata.Api`, `nirmata.Services`, `nirmata.Data`, `nirmata.Web`, `nirmata.Data.Dto`.

- Plane projects (`nirmata.Agents`) must not reference Product projects:
  - Agent orchestration is entirely separate from Product domain; Plane depends only on Engine (`nirmata.Aos`) and `nirmata.Common`.

- Product Application projects must not depend on **engine internals**:
  - If the product needs agent capabilities, it integrates **only via the host boundary** (`nirmata.Windows.Service.Api` over HTTP), not by compiling against `nirmata.Agents` or `nirmata.Aos`.

---

## 5) AOS workspace model (truth layers)
AOS uses `.aos/*` as a canonical workspace with **separated truth layers**:

- `.aos/spec/*` — intended truth (what should happen)
- `.aos/state/*` — operational truth (cursor, transitions, resumability)
- `.aos/evidence/*` — provable truth (runs/logs/artifacts/task-evidence)
- `.aos/codebase/*` — repository intelligence (how the repo works)
- `.aos/context/*` — deterministic context packs (bounded run inputs)
- `.aos/cache/*` — non-authoritative operational support (locks/tmp)

### What goes where (enforced by intent)
- If you are defining work, it belongs in **spec**.
- If you are tracking “where we are now”, it belongs in **state**.
- If you are proving something ran or passed checks, it belongs in **evidence**.
- If you are capturing repo understanding, it belongs in **codebase**.
- If you are preparing bounded inputs for a run, it belongs in **context**.
- If it’s disposable performance/concurrency hygiene, it belongs in **cache**.

---

## 6) Workflow planes (Plane behavior)
Agent workflows (`nirmata.Agents`) operate in distinct planes (each plane writes to different AOS layers):

- **Control Plane (routing + gating)**
  - Classify → gate → dispatch → validate → persist → next

- **Planning Plane (spec-first authoring)**
  - Project spec → roadmap → phase plans → task plans

- **Execution Plane**
  - Execute task plans sequentially, scoped edits only, verification per task

- **Verification & Fix Plane**
  - UAT gate produces pass/fail + scoped issues; failures generate fix plans

- **Continuity Plane**
  - Pause/resume and progress reporting driven by state + evidence, not chat

- **Brownfield Plane**
  - Codebase scan/map produces codebase intelligence pack for grounded planning

- **Scope Management Plane**
  - Roadmap/phase insert/remove while preserving cursor integrity

- **Backlog Capture & Triage Plane**
  - Capture todos without interrupting flow; triage deferred issues safely

---

## 7) Product domain notes (Project/Step)
- `Project` has many `Step` items (one-to-many).
- Cascade delete: removing a project removes its steps.

This is product-domain behavior and lives in `nirmata.Data` + `nirmata.Services` (not in engine).

---

## 8) Tech stack & conventions (product + shared)
- .NET 10 (C#)
- ASP.NET Core Web API with OpenAPI
- EF Core 9 + SQLite (lazy-loading proxies enabled)
- AutoMapper for entity-to-DTO mapping
- Razor Pages web app

### Code style
- Nullable reference types enabled; implicit usings on
- PascalCase public API; camelCase locals/params
- Async methods use `Async` suffix
- Data annotations for model validation/schema where appropriate
- XML comments for DTOs and public APIs where helpful

---

## 9) “Where does this belong?” quick test
Before adding a class/file, answer:

1) Is this about **product behavior** (users/data/features)?  
   → Product projects (`Api/Services/Data/Web/Dto`)

2) Is this about **workflow orchestration, plans, runs, evidence, validation, resumability**?  
   → Engine (`Aos/Agents/Windows.Service*`) + `.aos/*`

If it touches both, enforce an interface boundary: product stays product; engine stays engine.
