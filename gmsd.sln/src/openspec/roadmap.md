# GMSD.sln — Massive Solution Roadmap (Engine / Plane / Product)

This roadmap is organized into three top-level tracks:

- **Engine** — foundational AOS system (**`Gmsd.Aos`**)
- **Plane** — agent plane responsible for workflows, subagents, orchestration (**`Gmsd.Agents`** + hosts)
- **Product** — UI + product API + product services + product database + DTOs (**`Gmsd.Web`, `Gmsd.Api`, `Gmsd.Services`, `Gmsd.Data`, `Gmsd.Data.Dto`**)

Each **phase** below is formatted as a Markdown checkbox.  
Each **task bullet** under a phase is intended to become an OpenSpec proposal.

### Phase detail template (applies to all PH-* items)

Each `PH-*` phase includes these **inline** details (in addition to the per-task `Scope/Outputs/Verify` bullets):

- **Projects**: which `.csproj` owns the phase work
- **Code paths (target)**: the concrete folders/files expected to be created/changed
- **Workspace outputs (target)**: the concrete `.aos/**` artifact tree the phase creates/updates (when applicable)
- **Example artifacts**: 1–2 representative file paths for the phase’s key outputs
- **Verify**: the phase-level verification command(s)/checks (summary of the per-task verify bullets)

---

## Global invariants (apply to all phases)

### Separation of concerns (hard rules)
1) **Engine — `Gmsd.Aos`**

Owns (authoritative):

- `.aos/` workspace contract + canonical directory layout
- Path mapping + scope resolution (repo root ↔ `.aos/*`)
- Deterministic JSON I/O (stable ordering, canonical formatting, atomic writes)
- Embedded schemas + schema registry + validators
- Core primitives: Spec, State, Events/History, Evidence, Codebase access, Context pack building
- Tool contracts (process execution, filesystem, git abstraction, etc.)
- Evidence capture interfaces (for LLM calls, tool calls, etc.)

Exposes (only thing others compile against):

- A stable public surface: `Gmsd.Aos/Public/**`

Must NOT own:

- Agent workflows, prompting, orchestration, “plan/execute/verify” logic, product business logic/UI

2) **Plane — `Gmsd.Agents`**

Owns (authoritative):

- All orchestration workflows (“agents”): planning, execution, verification/UAT, fix-loop, continuity/pause-resume, brownfield, delegation
- Policy enforcement at runtime using Engine public APIs (gates, transitions, allowed operations)
- Produces plans, emits tasks, calls tools via Engine contracts, writes evidence/state through Engine
- **LLM provider abstractions** (ILlmProvider, message types, tool definitions, adapters)
- **Prompt template loading and management**

Must NOT own:

- Workspace/file format definitions, schema definitions, deterministic IO primitives (those live in Engine)

3) **Product domain (UI/API/Services/Data)**

Projects (authoritative):

- API: `Gmsd.Api`
- Services: `Gmsd.Services`
- Data access: `Gmsd.Data`
- DTOs: `Gmsd.Data.Dto`
- Web UI: `Gmsd.Web`
- Shared cross-cutting library: `Gmsd.Common`

Owns:

- Product UI + Product REST API + product services/data/business rules

Must NOT:

- Embed AOS planning/execution logic directly
- Call LLM orchestration directly from Product

Integration rule:

- Product may integrate with Plane through a hosted Plane API (see Windows Service API) when automation/orchestration is needed.

4) **Plane host + Windows service boundary (runtime hosting)**

Host projects:

- Windows Service host: `Gmsd.Windows.Service`
- Plane host API: `Gmsd.Windows.Service.Api`

Owns:

- Hosting/lifecycle (start/stop, background processing, scheduling hooks)
- Exposing Plane capabilities as a service boundary (HTTP/gRPC as defined)

Must NOT:

- Duplicate Engine primitives or Product business logic

### Project inventory (solution-wide)

- `Gmsd.Aos` (Engine)
- `Gmsd.Agents` (Plane)
- `Gmsd.Api` (Product API)
- `Gmsd.Services` (Product services/business logic)
- `Gmsd.Data` (Product persistence/data access)
- `Gmsd.Data.Dto` (DTO contracts)
- `Gmsd.Web` (Product UI)
- `Gmsd.Common` (Shared utilities/types used across Product; avoid leaking orchestration here)
- `Gmsd.Windows.Service` (Plane host/runtime)
- `Gmsd.Windows.Service.Api` (Plane host API surface)

### Workflow invariants (non-negotiable)

- **Spec-first gating** enforced by Orchestrator: spec → roadmap → plan → execute → verify → fix (no skipping transitions).
- **One atomic task = one commit**, with scope-restricted staging (out-of-scope diffs hard-fail and trigger re-plan).
- Evidence is append-only and always written under `.aos/evidence/runs/RUN-*`; tasks store pointers to their evidence.
- **No multiproject support:** one repo root == one `.aos/` workspace == one spec/project.json.
- All writes to `.aos/*` are deterministic + schema-validated (invalid artifacts never persist as “current state”).

---

# 1) ENGINE ROADMAP (Gmsd.Aos)

> Goal: make `Gmsd.Aos` a programmable spec/state engine with deterministic artifacts + validation + contracts that Plane workflows can rely on.

---

## MS-ENG-0001 — Engine Public Surface + Contract Boundary

- [x] **PH-ENG-0001 — Public API shape + “compile-against” contract**  
  **Outcome:** `Gmsd.Aos/Public/**` becomes the only stable surface other projects compile against; internal engine remains swappable.
  - **Projects:** `Gmsd.Aos`
  - **Code paths (target):**
    - `Gmsd.Aos/Public/**`
    - `Gmsd.Aos/Public/Services/**`
    - `Gmsd.Aos/Public/Catalogs/**`
    - `Gmsd.Aos/Contracts/**`
    - `Gmsd.Aos/Engine/**` (internal-only implementation)
    - `Gmsd.Aos/_Shared/**` (internal-only implementation)
  - **Workspace outputs (target):** *(none — API boundary and internals only)*
  - **Example artifacts:** *(n/a)*
  - **Verify:** `dotnet build Gmsd.Aos.csproj`
  - Define Engine Public surface skeleton  
    - **Scope:** `Gmsd.Aos/Public/**`, `Gmsd.Aos/Public/Services/**`, `Gmsd.Aos/Public/Catalogs/**`, `Gmsd.Aos/Contracts/**`  
    - **Outputs:** public interfaces for Workspace, SpecStore, StateStore, EvidenceStore, Validator, CommandRouter; Catalog stubs (SchemaIds, CommandIds, ArtifactKinds)  
    - **Verify:** `dotnet build Gmsd.Aos.csproj`
  - Introduce internal “Engine Core” namespaces  
    - **Scope:** `Gmsd.Aos/Engine/**`, `Gmsd.Aos/_Shared/**`  
    - **Outputs:** internal implementations hidden behind Public interfaces  
    - **Verify:** build passes; no public leakage
  - Enforce “no direct internal reference” rule  
    - **Scope:** `Gmsd.Aos.csproj`, `Gmsd.Aos/Public/**`  
    - **Outputs:** analyzer or visibility policy (tests only as needed)  
    - **Verify:** build + tests compile

---

## MS-ENG-0002 — Workspace + Paths + Deterministic JSON IO

- [x] **PH-ENG-0002 — Workspace root + canonical path mapping**  
  **Outcome:** Engine opens repo-root workspace, finds/creates `.aos/`, maps IDs → canonical files predictably.
  - **Projects:** `Gmsd.Aos`
  - **Code paths (target):**
    - `Gmsd.Aos/Workspace/**`
    - `Gmsd.Aos/Paths/**`
    - `Gmsd.Aos/Paths/Artifacts/**`
    - `Gmsd.Aos/Serialization/**`
  - **Workspace outputs (target):**
    ```text
    .aos/
      cache/
      codebase/
      config/
      context/
      evidence/
      locks/
      schemas/
      spec/
      state/
    ```
  - **Example artifacts:**
    - `.aos/spec/` (directory exists even before spec artifacts are authored)
    - `.aos/state/` (directory exists even before state artifacts are seeded)
  - **Verify:** create `.aos/` in a temp repo; deterministic path mapping tests; deterministic JSON write-read-write byte equality
  - Workspace discovery + `.aos` root contract  
    - **Scope:** `Gmsd.Aos/Workspace/**`, `Gmsd.Aos/Paths/**`  
    - **Outputs:** `IWorkspace` + repo root discovery + single-workspace enforcement  
    - **Verify:** temp repo init creates `.aos/`
  - Artifact path resolver (IDs → paths)  
    - **Scope:** `Gmsd.Aos/Paths/Artifacts/**`  
    - **Outputs:** canonical mappings for spec/state/evidence/codebase/context  
    - **Verify:** deterministic mapping tests
  - Deterministic JSON read/write policy  
    - **Scope:** `Gmsd.Aos/Serialization/**`  
    - **Outputs:** stable formatting/order; atomic write (temp + replace)  
    - **Verify:** write-read-write yields identical bytes

---

## MS-ENG-0003 — Embedded Schemas + Validation Gate

- [x] **PH-ENG-0003 — Schema registry + schema validator**  
  **Outcome:** engine loads embedded schemas by ID and validates artifacts structurally + cross-file invariants.
  - **Projects:** `Gmsd.Aos`
  - **Code paths (target):**
    - `Gmsd.Aos/Resources/Schemas/**` (embedded `*.schema.json`)
    - `Gmsd.Aos/Validation/Schemas/**` (schema registry/loader)
    - `Gmsd.Aos/Validation/**` (structural + invariant validation)
    - `Gmsd.Aos/Spec/Indexes/**` (invariant support / reference resolution)
  - **Workspace outputs (target):** *(none — validation operates over `.aos/**` artifacts but schemas are embedded in the assembly)*
  - **Example artifacts:**
    - `Gmsd.Aos/Resources/Schemas/project.schema.json`
    - `Gmsd.Aos/Resources/Schemas/workspace-lock.schema.json`
  - **Verify:** schema load by ID; invalid JSON yields normalized report; broken cross-file refs fail deterministically
  - Embed schema assets + schema loader  
    - **Scope:** `Gmsd.Aos/Validation/Schemas/**`, `Gmsd.Aos/Resources/Schemas/**`  
    - **Outputs:** embedded `*.schema.json` for: project, roadmap, milestone, phase, task, uat, issue, event, context-pack, evidence  
    - **Verify:** schema load by ID; missing fails fast
  - Structural validation engine  
    - **Scope:** `Gmsd.Aos/Validation/**`  
    - **Outputs:** validator API + normalized report format  
    - **Verify:** invalid JSON fails with normalized report
  - Cross-file invariants  
    - **Scope:** `Gmsd.Aos/Validation/**`, `Gmsd.Aos/Spec/Indexes/**`  
    - **Outputs:** roadmap refs exist, cursor refs exist, task plan scope resolves  
    - **Verify:** invariants pass on fixture workspace; fail on broken refs

---

## MS-ENG-0004 — Spec/State/Evidence Stores

- [x] **PH-ENG-0004 — Intended truth (spec) CRUD + indexing**  
  **Outcome:** deterministic read/write APIs for spec artifacts under `.aos/spec/**`.
  - **Projects:** `Gmsd.Aos`
  - **Code paths (target):**
    - `Gmsd.Aos/Spec/**`
    - `Gmsd.Aos/Spec/Indexes/**`
  - **Workspace outputs (target):**
    ```text
    .aos/
      spec/
        project.json
        roadmap.json
        milestones/
          index.json
        phases/
          index.json
        tasks/
          TSK-0001/
            task.json
            plan.json
            links.json
        issues/
          index.json
          ISS-0001.json
        uat/
          index.json
          UAT-0001.json
    ```
  - **Example artifacts:**
    - `.aos/spec/project.json`
    - `.aos/spec/tasks/TSK-0001/plan.json`
  - **Verify:** create/update/show/list flows; delete index then rebuild produces stable, deterministic entries
  - SpecStore CRUD (project/roadmap/milestone/phase/task/issue/uat)  
    - **Scope:** `Gmsd.Aos/Spec/**`, `Gmsd.Aos/Spec/Indexes/**`  
    - **Outputs:** CRUD APIs + stable indexes (tasks/phases/milestones/issues/uat)  
    - **Verify:** create/update/show/list flows pass
  - Index repair utility  
    - **Scope:** `Gmsd.Aos/Spec/Indexes/**`  
    - **Outputs:** rebuild indexes from filesystem  
    - **Verify:** delete index then rebuild reproduces expected entries

- [x] **PH-ENG-0005 — Operational truth (state + events)**  
  **Outcome:** `.aos/state/state.json` + `.aos/state/events.ndjson` managed via StateStore.
  - **Projects:** `Gmsd.Aos`
  - **Code paths (target):**
    - `Gmsd.Aos/State/**`
    - `Gmsd.Aos/Models/State/**`
    - `Gmsd.Aos/State/Events/**`
  - **Workspace outputs (target):**
    ```text
    .aos/
      state/
        state.json
        events.ndjson
    ```
  - **Example artifacts:**
    - `.aos/state/state.json`
    - `.aos/state/events.ndjson`
  - **Verify:** appending events updates derived state deterministically; event tailing returns stable order
  - StateStore + cursor model  
    - **Scope:** `Gmsd.Aos/State/**`, `Gmsd.Aos/Models/State/**`  
    - **Outputs:** cursor with milestone/phase/task/step + statuses  
    - **Verify:** append event updates derived state deterministically
  - Event append + tail reader  
    - **Scope:** `Gmsd.Aos/State/Events/**`  
    - **Outputs:** NDJSON append w/ schema validation; tail with filters  
    - **Verify:** append then tail returns expected order

- [x] **PH-ENG-0006 — Provable truth (evidence)**  
  **Outcome:** Engine creates RUN folders, attaches logs/artifacts, maintains task-evidence pointers.
  - **Projects:** `Gmsd.Aos`
  - **Code paths (target):**
    - `Gmsd.Aos/Evidence/**`
    - `Gmsd.Aos/Evidence/TaskEvidence/**`
    - `Gmsd.Aos/Paths/Artifacts/**`
  - **Workspace outputs (target):**
    ```text
    .aos/
      evidence/
        runs/
          RUN-*/
            commands.json
            summary.json
            logs/
              tool.log
            artifacts/
              diff.patch
        task-evidence/
          TSK-0001/
            latest.json
    ```
  - **Example artifacts:**
    - `.aos/evidence/runs/RUN-*/summary.json`
    - `.aos/evidence/task-evidence/TSK-0001/latest.json`
  - **Verify:** run start/finish creates the expected tree; `latest.json` updates atomically and remains schema-valid
  - Run lifecycle storage  
    - **Scope:** `Gmsd.Aos/Evidence/**`, `Gmsd.Aos/Paths/Artifacts/**`  
    - **Outputs:** `runs/RUN-*/commands.json`, `logs/`, `artifacts/`, `summary.json`  
    - **Verify:** run start/finish creates expected tree
  - Task-evidence latest pointer  
    - **Scope:** `Gmsd.Aos/Evidence/TaskEvidence/**`  
    - **Outputs:** `.aos/evidence/task-evidence/TSK-*/latest.json` updated (commit hash slot, diffstat slot)  
    - **Verify:** atomic update; schema-valid

---

## MS-ENG-0005 — Context Packs + Cache/Locks

- [x] **PH-ENG-0007 — Context pack builder**  
  **Outcome:** deterministic, budgeted context packs under `.aos/context/packs/**` (task/phase modes).
  - **Projects:** `Gmsd.Aos`
  - **Code paths (target):**
    - `Gmsd.Aos/Context/**`
    - `Gmsd.Aos/Context/Packs/**`
    - `Gmsd.Aos/Validation/Schemas/**` (context pack schema integration)
  - **Workspace outputs (target):**
    ```text
    .aos/
      context/
        packs/
          PCK-0001.json
    ```
  - **Example artifacts:**
    - `.aos/context/packs/PCK-0001.json`
    - `.aos/spec/tasks/TSK-0001/plan.json` (input that drives “task-mode” packing)
  - **Verify:** context pack validates against embedded schema; pack contains only allowed artifacts for the requested mode
  - Context pack schema + writer  
    - **Scope:** `Gmsd.Aos/Context/**`, `Gmsd.Aos/Validation/Schemas/**`  
    - **Outputs:** embedded context-pack schema + pack writer API  
    - **Verify:** pack validates
  - Pack build for task + phase  
    - **Scope:** `Gmsd.Aos/Context/Packs/**`  
    - **Outputs:** library handlers matching `pack build --task/--phase`  
    - **Verify:** pack includes only allowed artifacts

- [x] **PH-ENG-0008 — Cache + locks hygiene**  
  **Outcome:** `.aos/cache` and `.aos/locks` are safe, disposable, concurrency-aware.
  - **Projects:** `Gmsd.Aos`
  - **Code paths (target):**
    - `Gmsd.Aos/Cache/**`
    - `Gmsd.Aos/Cache/Locks/**`
  - **Workspace outputs (target):**
    ```text
    .aos/
      cache/
        (disposable cache entries)
      locks/
        workspace.lock
    ```
  - **Example artifacts:**
    - `.aos/locks/workspace.lock`
    - `.aos/cache/symbols.json` *(example disposable cache file; content is non-authoritative)*
  - **Verify:** second lock acquisition fails deterministically; cache clear/prune does not break validate/spec/state
  - Lock manager  
    - **Scope:** `Gmsd.Aos/Cache/Locks/**`  
    - **Outputs:** lock list/release primitives; prevent concurrent mutation  
    - **Verify:** second lock acquisition fails deterministically
  - Cache clear/prune  
    - **Scope:** `Gmsd.Aos/Cache/**`  
    - **Outputs:** clear + prune behaviors  
    - **Verify:** cache removal does not break validate/spec/state

---

## MS-ENG-0006 — Tool System + LLM Adapters

- [x] **PH-ENG-0009 — Tool contracts + registry**  
  **Outcome:** Engine defines tool invocation contract, descriptors, and registry (including MCP boundary).
  - **Projects:** `Gmsd.Aos`
  - **Code paths (target):**
    - `Gmsd.Aos/Contracts/Tools/**`
    - `Gmsd.Aos/Tools/**`
    - `Gmsd.Aos/Registry/**`
    - `Gmsd.Aos/Public/Catalogs/**`
    - `Gmsd.Aos/Mcp/**`
  - **Workspace outputs (target):** *(none — tools are invoked by Plane; evidence capture is handled via the EvidenceStore phase)*  
  - **Example artifacts:**
    - `Gmsd.Aos/Contracts/Tools/` *(request/result shapes)*
    - `Gmsd.Aos/Public/Catalogs/` *(stable tool IDs / enumeration surface)*
  - **Verify:** registry register/resolve works; tool catalog enumeration order is stable; MCP adapter stub invoke yields normalized result
  - Tool contract + descriptor models  
    - **Scope:** `Gmsd.Aos/Contracts/Tools/**`, `Gmsd.Aos/Tools/**`  
    - **Outputs:** `ITool`, request/result shapes, metadata model  
    - **Verify:** registry register + resolve by ID/name
  - Tool registry + catalog  
    - **Scope:** `Gmsd.Aos/Registry/**`, `Gmsd.Aos/Public/Catalogs/**`  
    - **Outputs:** stable tool catalog; deterministic enumeration  
    - **Verify:** list order stable
  - MCP adapter boundary  
    - **Scope:** `Gmsd.Aos/Mcp/**`  
    - **Outputs:** MCP endpoint wrapped as `ITool`  
    - **Verify:** stub invoke returns normalized result

- [x] **PH-ENG-0010 — Semantic Kernel integration for LLM orchestration (in Gmsd.Agents)**  
  **Outcome:** provider-agnostic LLM interface + adapters + prompt assets outside code.
  - **Projects:** `Gmsd.Agents`
  - **Code paths (target):**
    - `Gmsd.Agents/Configuration/SemanticKernelOptions.cs`
    - `Gmsd.Agents/Configuration/SemanticKernelServiceCollectionExtensions.cs`
    - `Gmsd.Agents/Execution/ControlPlane/Llm/Filters/AosEvidenceFunctionFilter.cs`
    - `Gmsd.Agents/Execution/ControlPlane/Llm/Tools/ToolToKernelFunctionAdapter.cs`
    - `Gmsd.Agents/Execution/ControlPlane/Llm/Tools/KernelPluginFactory.cs`
    - `Gmsd.Agents/Execution/ControlPlane/Llm/Prompts/SemanticKernelPromptFactory.cs`
    - `Gmsd.Agents/Resources/Prompts/**`
  - **Workspace outputs (target):** *(none — config binding/DI selects provider; prompting assets are loaded from code/resources as defined)*  
  - **Example artifacts:**
    - `Gmsd.Agents/Configuration/SemanticKernelOptions.cs` *(SK configuration options for all providers)*
    - `Gmsd.Agents/Resources/Prompts/*.prompt.yaml` *(SK-compatible prompt templates)*
  - **Verify:** SK configuration binds correctly; `Kernel` resolves from DI; provider-specific services (OpenAI, Azure OpenAI, Ollama) resolve; evidence filter captures LLM calls; prompt templates load with SK syntax (`{{$variable}}`)
  - Semantic Kernel configuration and DI registration
    - **Scope:** `Gmsd.Agents/Configuration/SemanticKernelOptions.cs`, `Gmsd.Agents/Configuration/SemanticKernelServiceCollectionExtensions.cs`
    - **Outputs:** `IOptions<SemanticKernelOptions>`; `Kernel` registration; provider-specific chat completion services
    - **Verify:** SK configuration binds from `Agents:SemanticKernel:*`; `Kernel` resolves from DI
  - Provider connectors (OpenAI, Azure OpenAI, Ollama, Anthropic)
    - **Scope:** `Gmsd.Agents/Configuration/SemanticKernelServiceCollectionExtensions.cs` (provider-specific builder methods)
    - **Outputs:** `OpenAIPromptExecutionSettings`, `AzureOpenAIPromptExecutionSettings`, Ollama connector, custom Anthropic `IChatCompletionService`
    - **Verify:** DI resolves selected provider by config; `Agents:SemanticKernel:Provider` selects implementation
  - Evidence capture via SK function filter
    - **Scope:** `Gmsd.Agents/Execution/ControlPlane/Llm/Filters/AosEvidenceFunctionFilter.cs`
    - **Outputs:** `IFunctionInvocationFilter` implementation capturing LLM calls to `LlmCallEnvelope`
    - **Verify:** evidence written for each LLM invocation with timestamp, tokens, duration
  - Tool system integration with SK
    - **Scope:** `Gmsd.Agents/Execution/ControlPlane/Llm/Tools/ToolToKernelFunctionAdapter.cs`, `Gmsd.Agents/Execution/ControlPlane/Llm/Tools/KernelPluginFactory.cs`
    - **Outputs:** `ITool` to `KernelFunction` adapter; plugins for auto-function-calling
    - **Verify:** tools execute via SK auto-function-calling; results captured in evidence
  - Prompt template loading for SK
    - **Scope:** `Gmsd.Agents/Execution/ControlPlane/Llm/Prompts/SemanticKernelPromptFactory.cs`, `Gmsd.Agents/Resources/Prompts/**`
    - **Outputs:** SK-compatible prompt templates (`.prompt.txt`, `.prompt.yaml`) with `{{$variable}}` syntax
    - **Verify:** templates load by ID and render with SK prompt template engine

---

## MS-ENG-0007 — Command Surface (library command handlers)

- [x] **PH-ENG-0011 — Command router + command group handlers**  
  **Outcome:** Engine exposes handlers matching the AOS command catalog (even if CLI host is elsewhere).
  - **Projects:** `Gmsd.Aos`
  - **Code paths (target):**
    - `Gmsd.Aos/Public/Services/**` (command routing surface)
    - `Gmsd.Aos/Public/Catalogs/**` (command IDs/catalog)
    - `Gmsd.Aos/Engine/Commands/**` (command implementations)
    - `Gmsd.Aos/Engine/Commands/{Base,Validate,Spec,State,Runs}/**`
    - `Gmsd.Aos/Engine/Commands/Help/**`
  - **Workspace outputs (target):** *(varies by command; core commands primarily create/validate `.aos/**` artifacts defined by earlier phases)*  
  - **Example artifacts:**
    - `.aos/spec/project.json` *(created/updated by spec/init commands)*
    - `.aos/evidence/runs/RUN-*/commands.json` *(written when commands are executed with evidence enabled)*
  - **Verify:** unknown command yields structured error; init→validate works in harness; help output is generated from the command catalog
  - CommandRouter + CommandCatalog  
    - **Scope:** `Gmsd.Aos/Public/Services/**`, `Gmsd.Aos/Public/Catalogs/**`, `Gmsd.Aos/Engine/Commands/**`  
    - **Outputs:** route `{group, command}` → handler; stable command IDs  
    - **Verify:** unknown command → structured error
  - Implement core handler set (init/status/config/validate/spec/state/run)  
    - **Scope:** `Gmsd.Aos/Engine/Commands/{Base,Validate,Spec,State,Runs}/**`  
    - **Outputs:** library handlers for each command group  
    - **Verify:** init → validate happy path works in harness
  - Help renderer uses command catalog  
    - **Scope:** `Gmsd.Aos/Engine/Commands/Help/**`  
    - **Outputs:** help output generated from registered commands  
    - **Verify:** help includes spec/state/run/etc.

- [x] **PH-ENG-0012 — DI registration extension (`AddGmsdAos`)**  
  **Outcome:** Engine services registerable via `IServiceCollection` extension for direct consumption by Plane.
  - **Projects:** `Gmsd.Aos`
  - **Code paths (target):**
    - `Gmsd.Aos/Composition/ServiceCollectionExtensions.cs`
    - `Gmsd.Aos/Public/Services/**` (interface registrations)
  - **Workspace outputs (target):** *(none — composition only)*
  - **Example artifacts:**
    - `Gmsd.Aos/Composition/ServiceCollectionExtensions.cs` with `AddGmsdAos(this IServiceCollection)`
  - **Verify:** `Gmsd.Agents` can call `services.AddGmsdAos()` and resolve `ICommandRouter`, `IWorkspace`, `ISpecStore`, `IStateStore`, `IEvidenceStore`, `IValidator`
  - AddGmsdAos extension method  
    - **Scope:** `Gmsd.Aos/Composition/ServiceCollectionExtensions.cs`  
    - **Outputs:** register all Engine Public services with appropriate lifetimes (Singleton for stores/catalogs, Scoped for command context)  
    - **Verify:** `Gmsd.Agents` composition can resolve Engine services
  - Service lifetime conventions  
    - **Scope:** `Gmsd.Aos/Composition/**`  
    - **Outputs:** deterministic lifetime rules (catalogs/stores = Singleton; command handlers = transient/scoped per invocation)  
    - **Verify:** multiple command executions don't share mutable state
  - Configuration binding  
    - **Scope:** `Gmsd.Aos/Configuration/**`  
    - **Outputs:** `IOptions<AosOptions>` for engine configuration  
    - **Verify:** config flows from `Gmsd.Agents` appsettings to Engine

---

## MS-ENG-0008 — E2E Verification + Agent Integration Tests

- [x] **PH-ENG-0013 — AOS E2E verification + agent integration tests**  
  **Outcome:** a dedicated verification phase that proves `aos init` + the full control-loop (spec → roadmap → plan → execute → verify → fix) works end-to-end, using real filesystem artifacts under `.aos/` and run evidence.
  - **Projects:** `Gmsd.Aos.Tests`, `Gmsd.Agents.Tests`
  - **Code paths (target):**
    - `tests/Gmsd.Aos.Tests/E2E/**`
    - `tests/Gmsd.Agents.Tests/E2E/**`
    - `tests/TestTargets/**` (fixture repos)
  - **Workspace outputs (target):** *(test fixtures are disposable; real `.aos/` artifacts are created during tests)*
    ```text
    %TEMP%/fixture-*/
      .aos/
        schemas/
        spec/
        state/
        evidence/
        codebase/
        context/
        cache/
    ```
  - **Example artifacts:**
    - `.aos/spec/project.json` *(created during init)*
    - `.aos/evidence/runs/RUN-*/commands.json` *(captured during execution)*
    - `.aos/evidence/task-evidence/TSK-*/latest.json` *(task evidence pointers)*
  - **Verify:** `dotnet test` passes locally + CI; `[Trait("Category","E2E")]` tests can be optionally excluded in fast loops
  - **TSK-00A** — Target project test harness (TestTarget + fixtures)  
    - **Scope:** `tests/TestTargets/**`, `tests/Gmsd.Aos.Tests/E2E/Harness/**`  
    - **Outputs:** deterministic TestTarget repo + test harness utilities (`RunAos()`, `AssertAosLayout()`, `ReadState()`, `ReadEventsTail()`)  
    - **Verify:** `dotnet test` runs harness tests and proves TestTarget creation is stable across runs
    - **Note:** No changes to product apps (Web/API/Data) in this task. Test harness supports CLI process OR in-proc command router, validates artifacts on disk afterward.
  - **TSK-00B** — `aos init` end-to-end verification (workspace + validation)  
    - **Scope:** `tests/Gmsd.Aos.Tests/E2E/InitVerification/**`  
    - **Outputs:** E2E tests proving init creates valid workspace and passes validation gates  
    - **Verify:**  
      - Init creates workspace (assert `.aos/` layers exist: schemas, spec, state, evidence, context, codebase, cache)  
      - Init is idempotent (run twice, assert no destructive rewrite)  
      - Validation gates succeed (`aos validate schemas`, `aos validate state`, `aos validate evidence`)  
      - Optional: CLI help surface check (`aos version`, `aos help`)
  - **TSK-00C** — Full agent-plane E2E test (Orchestrator → subagents → verify → fix)  
    - **Scope:** `tests/Gmsd.Agents.Tests/E2E/ControlLoop/**`  
    - **Outputs:** deterministic E2E scenario proving full gating loop  
    - **Verify:** `[Trait("Category","E2E")]` test executes:
      1. **Bootstrap:** `aos init` → seed project + roadmap → validate spec → assert cursor at first milestone/phase
      2. **Plan:** Create 1 phase with 2 atomic tasks max, persist `task.json` + `plan.json` with explicit scope
      3. **Execute:** Run `execute-plan` through Orchestrator, assert: fresh subagent per step, only allowed files touched, evidence written to `.aos/evidence/runs/RUN-*`, if git enabled: "one task = one commit"
      4. **Verify:** Run `verify-work` via UAT Verifier with intentional controlled failure, assert `ISS-*.json` created + UAT artifacts persisted
      5. **Fix:** Run Fix Planner → execute fix tasks → re-run verify-work → pass, assert cursor updated to verified-pass

---

# 2) PLANE ROADMAP (Gmsd.Agents + Hosts)

> Goal: implement specialist agent workflows that call Engine services, enforce strict scope, evidence, and resumability.

---

## MS-PLN-0001 — Agent Plane Composition Root + Runtime Skeleton

- [x] **PH-PLN-0001 — `Gmsd.Agents` DI + configuration baseline**  
  **Outcome:** hosts wire up workflows consistently via one composition root; consumes Engine services directly (not via CLI).
  - **Projects:** `Gmsd.Agents`
  - **Code paths (target):**
    - `Gmsd.Agents/Composition/**`
    - `Gmsd.Agents/Configuration/**`
    - `Gmsd.Agents/Models/**`
    - `Gmsd.Agents/Persistence/**`
    - `Gmsd.Agents/Observability/**`
    - `Gmsd.Agents/Program.cs`
    - `Gmsd.Agents/appsettings.json`
  - **Workspace outputs (target):** *(none — composition/runtime scaffolding only; actual `.aos/**` writes happen when workflows execute)*
  - **Example artifacts:** *(n/a)*
  - **Verify:** `dotnet build Gmsd.Agents.csproj`; log correlation ID format includes `RUN-*` when executing a run
  - AddGmsdAgents composition root  
    - **Scope:** `Gmsd.Agents/Composition/**`, `Gmsd.Agents/Configuration/**`, `Gmsd.Agents/Program.cs`, `Gmsd.Agents/appsettings.json`  
    - **Outputs:** calls `AddGmsdAos()` to register Engine services, then registers Plane workflows  
    - **Verify:** `dotnet build Gmsd.Agents.csproj`
  - Runtime models + persistence abstractions  
    - **Scope:** `Gmsd.Agents/Models/**`, `Gmsd.Agents/Persistence/**`  
    - **Outputs:** run request/response models; wrappers over Engine stores  
    - **Verify:** compile + minimal smoke
  - Observability scaffolding  
    - **Scope:** `Gmsd.Agents/Observability/**`  
    - **Outputs:** structured logs, correlation ID = RUN-*  
    - **Verify:** logs include run id
  - **Note:** Plane consumes Engine via direct service calls (`ICommandRouter`, stores, validators) — NOT via CLI process invocation. This enables rich error handling, better observability, and lower overhead for high-frequency orchestration operations.

---

## MS-PLN-0002 — Control Plane Orchestrator (workflow router)

- [x] **PH-PLN-0002 — Orchestrator workflow implements gating + dispatch**  
  **Outcome:** “classify → gate → dispatch → validate → persist → next” orchestrator exists as a workflow calling Engine stores/validator **via injected services** (direct method calls, not CLI).
  - **Projects:** `Gmsd.Agents`
  - **Code paths (target):**
    - `Gmsd.Agents/Execution/Orchestrator/**`
    - `Gmsd.Agents/Persistence/**` (run lifecycle wrappers over Engine stores)
  - **Workspace outputs (target):**
    ```text
    .aos/
      evidence/
        runs/
          RUN-*/
            commands.json
            summary.json
            logs/
            artifacts/
      state/
        events.ndjson  (appends run lifecycle events as defined)
    ```
  - **Example artifacts:**
    - `.aos/evidence/runs/RUN-*/commands.json`
    - `.aos/evidence/runs/RUN-*/summary.json`
  - **Verify:** unit tests cover routing; simulated workspaces hit each gate; evidence folder is created for each run
  - Input classification + command normalization  
    - **Scope:** `Gmsd.Agents/Execution/Orchestrator/**`  
    - **Outputs:** CLI/freeform normalized into workflow intent  
    - **Verify:** unit tests for routing
  - Enforce gating rules  
    - **Scope:** `Gmsd.Agents/Execution/Orchestrator/**`  
    - **Outputs:** missing project → Interviewer; missing roadmap → Roadmapper; missing plan → Planner; else Executor; then Verifier; fail → FixPlanner  
    - **Verify:** simulated workspaces hit each gate correctly
  - Run record lifecycle integration  
    - **Scope:** `Gmsd.Agents/Persistence/**`, `Gmsd.Agents/Execution/Orchestrator/**`  
    - **Outputs:** open run → attach input → close run status  
    - **Verify:** evidence folder created correctly
  - **Integration approach:** Orchestrator injects `ICommandRouter`, `IWorkspace`, `ISpecStore`, `IStateStore`, `IValidator` and calls methods directly. No process spawning.

---

## MS-PLN-0003 — Planning Plane Workflows (spec authoring)

- [x] **PH-PLN-0003 — New-Project Interviewer workflow**  
  **Outcome:** writes `.aos/spec/project.json` as canonical project truth.
  - **Projects:** `Gmsd.Agents`
  - **Code paths (target):**
    - `Gmsd.Agents/Execution/Planning/NewProjectInterviewer/**`
  - **Workspace outputs (target):**
    ```text
    .aos/
      spec/
        project.json
      evidence/
        runs/
          RUN-*/
            artifacts/
              interview.transcript.md
              interview.summary.md
    ```
  - **Example artifacts:**
    - `.aos/spec/project.json`
    - `.aos/evidence/runs/RUN-*/artifacts/interview.summary.md`
  - **Verify:** `validate spec` passes after writing; interview evidence is attached under the run folder
  - Implement Interviewer workflow  
    - **Scope:** `Gmsd.Agents/Execution/Planning/NewProjectInterviewer/**`  
    - **Outputs:** Q&A → normalized requirements → project.json  
    - **Verify:** validate spec passes
  - Interview evidence capture  
    - **Scope:** `Gmsd.Agents/Execution/Planning/NewProjectInterviewer/**`  
    - **Outputs:** run evidence includes transcript/summary file  
    - **Verify:** evidence attached under RUN-*

- [x] **PH-PLN-0004 — Roadmapper workflow**  
  **Outcome:** writes `.aos/spec/roadmap.json` + initializes `.aos/state/*`.
  - **Projects:** `Gmsd.Agents`
  - **Code paths (target):**
    - `Gmsd.Agents/Execution/Planning/Roadmapper/**`
  - **Workspace outputs (target):**
    ```text
    .aos/
      spec/
        roadmap.json
        milestones/
          index.json
        phases/
          index.json
      state/
        state.json
        events.ndjson
    ```
  - **Example artifacts:**
    - `.aos/spec/roadmap.json`
    - `.aos/state/state.json`
  - **Verify:** `validate spec` and `validate state` pass; cursor is set to first phase and a `roadmap.created` event is appended
  - Implement Roadmapper workflow  
    - **Scope:** `Gmsd.Agents/Execution/Planning/Roadmapper/**`  
    - **Outputs:** milestone/phase skeleton; phase stubs  
    - **Verify:** validate spec + state pass
  - Seed state + events  
    - **Scope:** `.aos/state/state.json`, `.aos/state/events.ndjson`  
    - **Outputs:** cursor set to first phase + roadmap.created event  
    - **Verify:** state shows correct cursor

- [x] **PH-PLN-0005 — Phase Context Gatherer + Phase Planner workflows**  
  **Outcome:** decomposes phase into 2–3 atomic tasks with plan.json scopes and verification steps.
  - **Projects:** `Gmsd.Agents`
  - **Code paths (target):**
    - `Gmsd.Agents/Execution/Planning/PhasePlanner/**`
    - `Gmsd.Agents/Execution/Planning/PhasePlanner/ContextGatherer/**`
    - `Gmsd.Agents/Execution/Planning/PhasePlanner/Assumptions/**`
  - **Workspace outputs (target):**
    ```text
    .aos/
      spec/
        tasks/
          TSK-0001/
            task.json
            plan.json
            links.json
      state/
        events.ndjson  (appends planning decisions/assumptions as events/decisions as defined)
      evidence/
        runs/
          RUN-*/
            artifacts/
              assumptions.md
    ```
  - **Example artifacts:**
    - `.aos/spec/tasks/TSK-0001/plan.json`
    - `.aos/evidence/runs/RUN-*/artifacts/assumptions.md`
  - **Verify:** `plan.json` contains explicit file scopes + checks; planning decisions are persisted deterministically; assumptions snapshot is attached to evidence
  - Phase Context Gatherer  
    - **Scope:** `Gmsd.Agents/Execution/Planning/PhasePlanner/ContextGatherer/**`  
    - **Outputs:** phase brief persisted as decisions/events  
    - **Verify:** decisions appear in state
  - Phase Planner  
    - **Scope:** `Gmsd.Agents/Execution/Planning/PhasePlanner/**`  
    - **Outputs:** `.aos/spec/tasks/TSK-*/{task,plan,links}.json`  
    - **Verify:** plan.json contains explicit file scopes + checks
  - Phase Assumption Lister  
    - **Scope:** `Gmsd.Agents/Execution/Planning/PhasePlanner/Assumptions/**`  
    - **Outputs:** assumptions snapshot attached to evidence  
    - **Verify:** assumptions artifact appears under RUN

---

## MS-PLN-0004 — Scope + Roadmap Modularity

- [x] **PH-PLN-0006 — Roadmap Modifier / Phase Remover workflows**  
  **Outcome:** insert/remove phases safely, renumber consistently, keep cursor coherent.
  - **Projects:** `Gmsd.Agents`
  - **Code paths (target):**
    - `Gmsd.Agents/Execution/Planning/RoadmapModifier/**`
  - **Workspace outputs (target):**
    ```text
    .aos/
      spec/
        roadmap.json   (modified + renumbered)
        issues/
          ISS-0001.json  (created when removal is blocked, as defined)
      state/
        state.json     (cursor updates)
        events.ndjson  (roadmap.modified / blocker events)
    ```
  - **Example artifacts:**
    - `.aos/spec/roadmap.json`
    - `.aos/spec/issues/ISS-0001.json`
  - **Verify:** validate spec + state pass after insert/remove/renumber; active-phase removal without force produces a deterministic blocker/issue
  - Roadmap Modifier  
    - **Scope:** `Gmsd.Agents/Execution/Planning/RoadmapModifier/**`  
    - **Outputs:** add/insert/remove + renumber + cursor updates + roadmap.modified event  
    - **Verify:** validate spec + state pass
  - Phase Remover safety checks  
    - **Scope:** same as above  
    - **Outputs:** cannot remove active phase without explicit force flag; otherwise issue/blocker  
    - **Verify:** active removal attempt produces blocker

---

## MS-PLN-0005 — Execution Plane Workflows

- [x] **PH-PLN-0007 — Task Executor workflow**  
  **Outcome:** executes tasks sequentially, strict scope, fresh subagent per task, evidence capture.
  - **Projects:** `Gmsd.Agents`
  - **Code paths (target):**
    - `Gmsd.Agents/Execution/Execution/TaskExecutor/**`
    - `Gmsd.Agents/Execution/Execution/SubagentRuns/**`
  - **Workspace outputs (target):**
    ```text
    .aos/
      evidence/
        runs/
          RUN-*/
            logs/
            artifacts/
      evidence/
        task-evidence/
          TSK-0001/
            latest.json
      state/
        state.json     (task/step status updates)
        events.ndjson  (execution events)
    ```
  - **Example artifacts:**
    - `.aos/spec/tasks/TSK-0001/plan.json` *(input)*
    - `.aos/evidence/task-evidence/TSK-0001/latest.json` *(output pointer)*
  - **Verify:** executor applies changes strictly within task-scoped files; produces normalized result; updates cursor/task status deterministically; creates distinct RUN records per atomic task/step
  - Task Executor  
    - **Scope:** `Gmsd.Agents/Execution/Execution/TaskExecutor/**`  
    - **Outputs:** reads `.aos/spec/tasks/TSK-*/plan.json`, applies changes within allowed files only, captures evidence  
    - **Verify:** normalized result + cursor/task status updated
  - Subagent orchestration wrapper  
    - **Scope:** `Gmsd.Agents/Execution/Execution/SubagentRuns/**`  
    - **Outputs:** one RUN per atomic task/step; bounded context packs per step  
    - **Verify:** distinct RUN records produced

- [x] **PH-PLN-0008 — Atomic Git Committer workflow**  
  **Outcome:** stages only task-scoped files and commits with TSK-based messages.
  - **Projects:** `Gmsd.Agents`
  - **Code paths (target):**
    - `Gmsd.Agents/Execution/Execution/AtomicGitCommitter/**`
  - **Workspace outputs (target):**
    ```text
    .aos/
      evidence/
        runs/
          RUN-*/
            artifacts/
              git-diffstat.json
              git-commit.json
      evidence/
        task-evidence/
          TSK-0001/
            latest.json  (commit hash + diffstat slots)
    ```
  - **Example artifacts:**
    - `.aos/evidence/runs/RUN-*/artifacts/git-commit.json`
    - `.aos/evidence/task-evidence/TSK-0001/latest.json`
  - **Verify:** stage intersection(changed files, allowed scope) only; forbidden files never staged; evidence records commit hash + diffstat
  - Git commit workflow  
    - **Scope:** `Gmsd.Agents/Execution/Execution/AtomicGitCommitter/**`  
    - **Outputs:** commit + hash/diffstat recorded into evidence  
    - **Verify:** evidence includes commit metadata
  - Scope intersection staging  
    - **Scope:** same  
    - **Outputs:** stage intersection(changed files, allowed scope)  
    - **Verify:** forbidden files never staged

---

## MS-PLN-0006 — Verification + Fix Loop

- [x] **PH-PLN-0009 — UAT Verifier workflow**  
  **Outcome:** verify-work runs acceptance checks, writes issues, records pass/fail with evidence.
  - **Projects:** `Gmsd.Agents`
  - **Code paths (target):**
    - `Gmsd.Agents/Execution/Verification/UatVerifier/**`
  - **Workspace outputs (target):**
    ```text
    .aos/
      spec/
        uat/
          UAT-0001.json
        issues/
          ISS-0001.json
      evidence/
        runs/
          RUN-*/
            summary.json  (pass/fail + pointers)
            artifacts/
              uat-results.json
    ```
  - **Example artifacts:**
    - `.aos/spec/uat/UAT-0001.json`
    - `.aos/spec/issues/ISS-0001.json`
  - **Verify:** schema-valid UAT artifact is produced; failures create issues with repro/expected/actual; orchestrator routes to FixPlanner on failure
  - UAT checklist derived from acceptance criteria  
    - **Scope:** `Gmsd.Agents/Execution/Verification/UatVerifier/**`  
    - **Outputs:** UAT record stored under `.aos/spec/uat/` or task uat.json  
    - **Verify:** schema-valid UAT artifact + evidence
  - Issue creation on failure  
    - **Scope:** `.aos/spec/issues/**`  
    - **Outputs:** ISS-*.json with repro + expected vs actual + scope  
    - **Verify:** orchestrator routes to FixPlanner on fail

- [x] **PH-PLN-0010 — Fix Planner workflow**  
  **Outcome:** consumes UAT failures and generates 2–3 task fix plans with verification steps.
  - **Projects:** `Gmsd.Agents`
  - **Code paths (target):**
    - `Gmsd.Agents/Execution/FixPlanner/**`
  - **Workspace outputs (target):**
    ```text
    .aos/
      spec/
        tasks/
          TSK-0002/
            task.json
            plan.json
            links.json
      state/
        state.json     (cursor indicates ready-to-execute-fix)
        events.ndjson  (fix planning events)
    ```
  - **Example artifacts:**
    - `.aos/spec/issues/ISS-0001.json` *(input)*
    - `.aos/spec/tasks/TSK-0002/plan.json` *(output)*
  - **Verify:** validate spec passes; generated fix plans include explicit scope + verification checks
  - Fix plan generator  
    - **Scope:** `Gmsd.Agents/Execution/FixPlanner/**`  
    - **Outputs:** new fix task `TSK-*/plan.json` with explicit scope + checks  
    - **Verify:** validate spec passes; cursor indicates ready-to-execute-fix

---

## MS-PLN-0007 — Continuity + History

- [x] **PH-PLN-0011 — Pause/Resume Manager workflow**  
  **Outcome:** interruption-safe handoff snapshot and deterministic resume.
  - **Projects:** `Gmsd.Agents`
  - **Code paths (target):**
    - `Gmsd.Agents/Execution/Continuity/PauseResumeManager/**`
  - **Workspace outputs (target):**
    ```text
    .aos/
      state/
        handoff.json
      evidence/
        runs/
          RUN-*/  (used for resume-by-run)
    ```
  - **Example artifacts:**
    - `.aos/state/handoff.json`
    - `.aos/evidence/runs/RUN-*/summary.json`
  - **Verify:** resume reconstructs next action deterministically; resumed task respects the same scope constraints as the original
  - pause-work / resume-work  
    - **Scope:** `Gmsd.Agents/Execution/Continuity/PauseResumeManager/**`, `.aos/state/handoff.json`  
    - **Outputs:** handoff includes cursor, in-flight task, scope, next command  
    - **Verify:** resume reconstructs next action from artifacts
  - resume-task by RUN id  
    - **Scope:** same  
    - **Outputs:** locate RUN evidence and restore execution packet  
    - **Verify:** resumed task respects scope

- [x] **PH-PLN-0012 — Progress Reporter + History Writer workflows**  
  **Outcome:** deterministic progress report + durable narrative summary with evidence references.
  - **Projects:** `Gmsd.Agents`
  - **Code paths (target):**
    - `Gmsd.Agents/Execution/Continuity/ProgressReporter/**`
    - `Gmsd.Agents/Execution/Continuity/HistoryWriter/**`
  - **Workspace outputs (target):**
    ```text
    .aos/
      spec/
        summary.md
      evidence/
        summary.md  (optional alternative location as defined)
    ```
  - **Example artifacts:**
    - `.aos/spec/summary.md`
    - `.aos/evidence/runs/RUN-*/summary.json` *(linked from summaries)*
  - **Verify:** progress output matches state/roadmap/tasks deterministically; summary entries include evidence pointers and commit hashes (when available)
  - Progress Reporter  
    - **Scope:** `Gmsd.Agents/Execution/Continuity/ProgressReporter/**`  
    - **Outputs:** current cursor, blockers, next recommended command  
    - **Verify:** matches state/roadmap/tasks deterministically
  - History Writer  
    - **Scope:** `Gmsd.Agents/Execution/Continuity/HistoryWriter/**`, `.aos/spec/summary.md` (or `.aos/evidence/summary.md`)  
    - **Outputs:** append entry keyed by RUN/TSK + verification proof + commit hash  
    - **Verify:** summary links to evidence artifacts

---

## MS-PLN-0008 — Backlog Capture & Triage

- [x] **PH-PLN-0013 — Deferred Issues Curator + Todo capture/review**  
  **Outcome:** deferred queue separate from roadmap; triage routes urgent items into main loop.
  - **Projects:** `Gmsd.Agents`
  - **Code paths (target):**
    - `Gmsd.Agents/Execution/Backlog/DeferredIssuesCurator/**`
    - `Gmsd.Agents/Execution/Backlog/TodoCapturer/**`
    - `Gmsd.Agents/Execution/Backlog/TodoReviewer/**`
  - **Workspace outputs (target):**
    ```text
    .aos/
      context/
        todos/
          TODO-0001.json
      spec/
        issues/
          ISS-0001.json  (triage updates as applicable)
      state/
        events.ndjson  (triage/todo capture events)
    ```
  - **Example artifacts:**
    - `.aos/context/todos/TODO-0001.json`
    - `.aos/spec/issues/ISS-0001.json`
  - **Verify:** urgent issue yields a deterministic routing recommendation; TODO capture does not change cursor unless explicitly promoted
  - consider-issues triage  
    - **Scope:** `Gmsd.Agents/Execution/Backlog/DeferredIssuesCurator/**`, `.aos/spec/issues/**`  
    - **Outputs:** status/priority updates + triage event  
    - **Verify:** urgent issue yields routing recommendation
  - add-todo capture lane  
    - **Scope:** `Gmsd.Agents/Execution/Backlog/TodoCapturer/**`, `.aos/context/todos/**`  
    - **Outputs:** TODO-*.json created + event appended  
    - **Verify:** cursor unaffected
  - check-todos reviewer  
    - **Scope:** `Gmsd.Agents/Execution/Backlog/TodoReviewer/**`  
    - **Outputs:** selection routes into spec task or roadmap insertion  
    - **Verify:** creates task spec or inserts phase

---

## MS-PLN-0009 — Brownfield (existing codebase intelligence)

- [x] **PH-PLN-0014 — Codebase Mapper workflow**  
  **Outcome:** `.aos/codebase/**` intelligence pack + caches (symbols + file-graph).
  - **Projects:** `Gmsd.Agents`
  - **Code paths (target):**
    - `Gmsd.Agents/Execution/Brownfield/CodebaseMapper/**`
  - **Workspace outputs (target):**
    ```text
    .aos/
      codebase/
        map.json
        stack.json
        architecture.json
        structure.json
        conventions.json
        testing.json
        integrations.json
        concerns.json
        cache/
          symbols.json
          file-graph.json
    ```
  - **Example artifacts:**
    - `.aos/codebase/map.json`
    - `.aos/codebase/cache/symbols.json`
  - **Verify:** validate codebase passes; rebuilds are deterministic for the same repo state
  - codebase scan + map build equivalents  
    - **Scope:** `Gmsd.Agents/Execution/Brownfield/CodebaseMapper/**`, `.aos/codebase/**`  
    - **Outputs:** `map.json`, `stack.json`, `architecture.json`, `structure.json`, `conventions.json`, `testing.json`, `integrations.json`, `concerns.json`  
    - **Verify:** validate codebase passes
  - derived caches (symbols + file-graph)  
    - **Scope:** `.aos/codebase/cache/**`  
    - **Outputs:** `symbols.json`, `file-graph.json`  
    - **Verify:** rebuild deterministic

---

# 3) PRODUCT ROADMAP (UI + Product API + Services + Data + DTO)

> Goal: deliver a clean product stack that is non-engine, integrating to Plane via direct invocation for MVP, later via Windows Service API for production.

---

## MS-PRD-0001 — Product Data Baseline (`Gmsd.Data`)

- [x] **PH-PRD-0001 — EF Core context + entities + migrations skeleton**  
  **Outcome:** product DB exists with clean separation from `.aos/` artifacts.
  - **Projects:** `Gmsd.Data`
  - **Code paths (target):**
    - `Gmsd.Data/Context/**`
    - `Gmsd.Data/Entities/**`
    - `Gmsd.Data/Migrations/**`
  - **Workspace outputs (target):** *(product persistence artifacts; no `.aos/**` ownership)*
    ```text
    Gmsd.Data/
      Migrations/
        *_InitialCreate.cs
        *_InitialCreate.Designer.cs
        GmsdDbContextModelSnapshot.cs
      sqllitedb/
        Gmsd.db   (local/dev artifact as configured)
    ```
  - **Example artifacts:**
    - `Gmsd.Data/Migrations/*_InitialCreate.cs`
    - `Gmsd.Data/sqllitedb/Gmsd.db`
  - **Verify:** `dotnet build`; EF migration generation succeeds; sqlite schema matches the migration
  - DbContext + entity skeleton  
    - **Scope:** `Gmsd.Data/Context/**`, `Gmsd.Data/Entities/**`  
    - **Outputs:** initial entities + DbContext + config  
    - **Verify:** build + migration generation succeeds
  - Migrations baseline  
    - **Scope:** `Gmsd.Data/Migrations/**`  
    - **Outputs:** initial migration created and applies  
    - **Verify:** sqlite created and schema matches

---

## MS-PRD-0002 — DTO & Validation (`Gmsd.Data.Dto`)

- [x] **PH-PRD-0002 — DTO models + request validators**  
  **Outcome:** API boundary DTOs exist with validation.
  - **Projects:** `Gmsd.Data.Dto`
  - **Code paths (target):**
    - `Gmsd.Data.Dto/Models/**`
    - `Gmsd.Data.Dto/Requests/**`
    - `Gmsd.Data.Dto/Validators/**`
  - **Workspace outputs (target):** *(none — DTOs/validators are compiled outputs consumed by API/services)*
  - **Example artifacts:**
    - `Gmsd.Data.Dto/Models/**`
    - `Gmsd.Data.Dto/Validators/**`
  - **Verify:** compile passes; invalid DTOs fail validation as expected
  - DTO models + request shapes  
    - **Scope:** `Gmsd.Data.Dto/Models/**`, `Gmsd.Data.Dto/Requests/**`  
    - **Outputs:** DTOs for core entities  
    - **Verify:** compile passes
  - Validators  
    - **Scope:** `Gmsd.Data.Dto/Validators/**`  
    - **Outputs:** validation rules aligned with API constraints  
    - **Verify:** invalid DTO fails as expected

---

## MS-PRD-0003 — Product Services Tier (`Gmsd.Services`)

- [x] **PH-PRD-0003 — Services interfaces + implementations**  
  **Outcome:** application logic lives in `Gmsd.Services`, not controllers or UI.
  - **Projects:** `Gmsd.Services` (and mapping in `Gmsd.Data`)
  - **Code paths (target):**
    - `Gmsd.Services/Interfaces/**`
    - `Gmsd.Services/Implementations/**`
    - `Gmsd.Services/Composition/**`
    - `Gmsd.Data/Mapping/**` (DTO/entity mapping boundary)
  - **Workspace outputs (target):** *(none — compiled services consumed by `Gmsd.Api` and optionally `Gmsd.Web`)*
  - **Example artifacts:**
    - `Gmsd.Services/Composition/` *(e.g., `AddGmsdServices(...)`)*
    - `Gmsd.Services/Interfaces/` *(use-case contracts)*
  - **Verify:** `Gmsd.Api` resolves services via DI; service-level tests pass
  - Service interfaces  
    - **Scope:** `Gmsd.Services/Interfaces/**`  
    - **Outputs:** use-case interfaces  
    - **Verify:** build; no circular refs
  - Service implementations + mapping  
    - **Scope:** `Gmsd.Services/Implementations/**`, `Gmsd.Data/Mapping/**`  
    - **Outputs:** impls using Data; map to DTOs  
    - **Verify:** service-level tests pass
  - Services composition root  
    - **Scope:** `Gmsd.Services/Composition/**`  
    - **Outputs:** `AddGmsdServices(...)`  
    - **Verify:** `Gmsd.Api` resolves services

---

## MS-PRD-0004 — Product REST API (`Gmsd.Api`)

- [x] **PH-PRD-0004 — Versioned controllers + health**  
  **Outcome:** stable product API with clean controller/service split.
  - **Projects:** `Gmsd.Api`
  - **Code paths (target):**
    - `Gmsd.Api/Program.cs`
    - `Gmsd.Api/appsettings*.json`
    - `Gmsd.Api/Controllers/V1/**` *(preferred versioned controller location)*
    - `Gmsd.Api/Controllers/**` *(health/base controllers as needed)*
  - **Workspace outputs (target):** *(none — runtime is HTTP service; persistence lives in `Gmsd.Data`)*
  - **Example artifacts:**
    - `Gmsd.Api/Controllers/V1/ProjectController.cs` *(example v1 controller)*
    - `Gmsd.Api/Program.cs`
  - **Verify:** local run; swagger loads; integration tests hit v1 endpoints and health endpoints
  - API skeleton + DI wiring  
    - **Scope:** `Gmsd.Api/Program.cs`, `Gmsd.Api/appsettings*.json`  
    - **Outputs:** controllers, services/data DI, swagger  
    - **Verify:** local run; swagger loads
  - Core controllers (v1)  
    - **Scope:** `Gmsd.Api/Controllers/V1/**`, `Gmsd.Api/HealthController.cs`, `Gmsd.Api/GmsdController.cs`  
    - **Outputs:** baseline endpoints + health  
    - **Verify:** integration tests hit endpoints

---

## MS-PRD-0005 — Web UI (`Gmsd.Web`)

- [x] **PH-PRD-0005 — Razor Pages shell + assets + basic product views**  
  **Outcome:** UI shell shows product data and later agent run status.
  - **Projects:** `Gmsd.Web`
  - **Code paths (target):**
    - `Gmsd.Web/Pages/**`
    - `Gmsd.Web/Shared/Views/**`
    - `Gmsd.Web/wwwroot/**`
    - `Gmsd.Web/assets/**`
  - **Workspace outputs (target):** *(static assets and compiled web app; no `.aos/**` ownership)*
  - **Example artifacts:**
    - `Gmsd.Web/Pages/Index.cshtml` *(example)*
    - `Gmsd.Web/wwwroot/` *(static assets root)*
  - **Verify:** homepage renders; read-only list/detail pages render product data via services or API client
  - Layout + static assets  
    - **Scope:** `Gmsd.Web/Pages/**`, `Gmsd.Web/Shared/Views/**`, `Gmsd.Web/wwwroot/**`, `Gmsd.Web/assets/**`  
    - **Outputs:** base layout/nav, css/js pipeline, error pages  
    - **Verify:** homepage renders
  - Product data pages (read-only first)  
    - **Scope:** `Gmsd.Web/Pages/**`  
    - **Outputs:** list/detail pages for a core entity  
    - **Verify:** data renders via services or API client

---

## MS-PRD-0006 — Product MVP Integration (Direct Agent Invocation)

- [x] **PH-PRD-0006 — Direct agent runner for MVP (no Windows Service)**  
  **Outcome:** Product can invoke agents directly via `Gmsd.Agents` library for MVP/debugging.
  - **Projects:** `Gmsd.Web`, `Gmsd.Agents`
  - **Code paths (target):**
    - `Gmsd.Web/AgentRunner/**`
    - `Gmsd.Web/Composition/**`
  - **Workspace outputs (target):** *(no `.aos/**` writes from Product directly — `.aos/**` is written by Plane in the target workspace when runs execute)*
  - **Example artifacts:**
    - `Gmsd.Web/AgentRunner/WorkflowClassifier.cs`
    - `Gmsd.Web/Pages/Runs/Index.cshtml` *(runs dashboard)*
  - **Verify:** UI can start a run directly; runs dashboard displays run status/evidence
  - Direct agent runner (in-process)  
    - **Scope:** `Gmsd.Web/AgentRunner/**`  
    - **Outputs:** wrapper that calls `Gmsd.Agents` orchestrator directly (no HTTP/service boundary)  
    - **Verify:** can execute no-op command against a test workspace
  - Web UI "Runs" dashboard  
    - **Scope:** `Gmsd.Web/Pages/**`  
    - **Outputs:** run list + run detail (status/logs/artifacts pointers)  
    - **Verify:** displays run status correctly
  - DI wiring for direct mode  
    - **Scope:** `Gmsd.Web/Composition/**`  
    - **Outputs:** `AddGmsdAgents()` called directly in Web DI  
    - **Verify:** service starts without Windows Service dependency

---

## MS-PRD-0007 — Product Data Migration/Seeds

- [x] **PH-PRD-0007 — Initial data seeds + migration tooling**  
  **Outcome:** clean baseline data for product entities.
  - **Projects:** `Gmsd.Data`
  - **Code paths (target):**
    - `Gmsd.Data/Migrations/Seeds/**`
  - **Workspace outputs (target):** *(none — database seeds)*
  - **Example artifacts:**
    - `Gmsd.Data/Migrations/Seeds/InitialData.sql`
  - **Verify:** fresh database has seed data; migrations apply cleanly

---

## MS-PRD-0008 — AOS Engine UI Surface (MVP Pages)

- [x] **PH-PRD-0008 — Core UI Pages (Spec → Plan → Execute → Verify → Fix)**
  **Outcome:** minimum UI surface to make the AOS engine operational end-to-end.
  - **Projects:** `Gmsd.Web`
  - **Code paths (target):**
    - `Gmsd.Web/Pages/Workspace/**`
    - `Gmsd.Web/Pages/Dashboard/**`
    - `Gmsd.Web/Pages/Command/**`
    - `Gmsd.Web/Pages/Specs/**`
  - **Workspace outputs (target):** *(none — UI only)*
  - **Verify:** all 5 pages render without errors; navigation works

  - **1) Workspace Picker / Project Selector**
    - Set Project Target Repo Path (text input + browse)
    - Recent workspaces list (name, path, last opened, last run status)
    - Buttons: Open, Init (.aos), Validate workspace, Repair indexes
    - Workspace health summary: .aos present, schemas ok, locks present, last run
    - Persist selection in config + show current config
  - **2) Dashboard (Overview / "Where am I?")**
    - Current cursor (milestone / phase / task / step) from state.json
    - "Next recommended action" routing
    - Blockers + open issues summary
    - Quick actions: Validate, Checkpoint, Pause, Resume, Tail events
    - "Latest run" card(s): pass/fail, linked evidence artifacts
  - **3) Command Center (Modern Chat UI)**
    - Modern chat thread UI (messages, streaming output, run cards)
    - Slash commands: /init, /status, /validate, /spec, /run, /codebase, /pack, /checkpoint
    - Run summary per send: status, next action, evidence links
    - Inline rendering: JSON artifacts, validation reports, command logs
    - Attachments: evidence artifacts, linked files, context packs
    - Safety rails: show allowed scope / touched files for execution steps
  - **4) Specs Explorer (Artifact Browser)**
    - Tree + search across spec/ (project, roadmap, milestones, phases, tasks, issues, uat)
    - Dual-mode editing: Form editor + Raw JSON + Validate button
    - "Open on disk path" link for each artifact
    - Diff viewer vs previous version
  - **5) Project Spec Page (spec/project.json)**
    - View/edit project definition + constraints + success criteria
    - "Interview mode" UI that writes to spec/project.json
    - Validate + show schema errors
    - Export/import project spec

- [x] **PH-PRD-0009 — Execution & Verification UI Pages**
  **Outcome:** execution tracking, verification, and issue management UI.
  - **Projects:** `Gmsd.Web`
  - **Code paths (target):**
    - `Gmsd.Web/Pages/Roadmap/**`
    - `Gmsd.Web/Pages/Milestones/**`
    - `Gmsd.Web/Pages/Phases/**`
    - `Gmsd.Web/Pages/Tasks/**`
    - `Gmsd.Web/Pages/Runs/**`
    - `Gmsd.Web/Pages/Uat/**`
    - `Gmsd.Web/Pages/Issues/**`
  - **Workspace outputs (target):** *(none — UI only)*
  - **Verify:** all pages render spec artifacts correctly; actions wired to backend

  - **6) Roadmap Page (spec/roadmap.json)**
    - Timeline/ordered list of milestones + phases
    - Controls: Add / Insert / Remove phase, auto reindex + validate
    - "Discuss phase" + "Plan phase" entry points
    - Show roadmap ↔ state cursor alignment warnings
  - **7) Milestones Page (spec/milestones/**)**
    - List/create/update milestones
    - Milestone detail: phases, status, completion gate
    - Actions: New milestone, Complete current
  - **8) Phases Page (spec/phases/**)**
    - Phase detail: goals/outcomes, assumptions, research, tasks
    - Actions: List assumptions → persist, Set research → persist
    - Plan phase → generate 2–3 atomic tasks + plans
    - Show phase constraints pulled from state decisions/blockers
  - **9) Tasks Page (spec/tasks/TSK-*/…)**
    - Task list with filters (phase, milestone, status)
    - Task detail tabs: task.json, plan.json, uat.json, links
    - Actions: Execute plan, View evidence for latest run, Mark status
  - **10) Execution / Runs Page (evidence/runs/**)**
    - Runs list (RUN-*, status, timestamp, task/phase association)
    - Run detail: commands executed + logs, artifacts attached, verification outputs, files changed + commit hash
  - **11) Verification / UAT Page (spec/uat/** + task uat)**
    - "Verify work" wizard: builds checklist from task acceptance criteria
    - Records pass/fail + repro notes, emits issues on fail
    - Re-run verification against same checks
    - Links: UAT ↔ issues ↔ runs evidence
  - **12) Issues Page (spec/issues/**)**
    - List/filter by status/type/severity/task/phase/milestone
    - Issue detail: repro steps, expected vs actual, impacted area/scope
    - Actions: Route to fix plan, mark resolved/deferred, link to task/phase

- [x] **PH-PRD-0010 — Advanced UI Pages (Fix Planning, Intelligence, State)**
  **Outcome:** fix planning, codebase intelligence, context packs, and state management UI.
  - **Projects:** `Gmsd.Web`
  - **Code paths (target):**
    - `Gmsd.Web/Pages/Fix/**`
    - `Gmsd.Web/Pages/Codebase/**`
    - `Gmsd.Web/Pages/Context/**`
    - `Gmsd.Web/Pages/State/**`
    - `Gmsd.Web/Pages/Validation/**`
  - **Workspace outputs (target):** *(none — UI only)*
  - **Verify:** advanced pages render correctly; checkpoint/restore works

  - **13) Fix Planning Page (Repair Loop)**
    - Show generated fix plan tasks (max small set)
    - Actions: Plan fix, Execute fix, Re-verify
    - Clear loop state (verified-pass vs verified-fail)
  - **14) Codebase Intelligence Page (codebase/**)**
    - Trigger: scan / map build / symbols build / graph build
    - Viewer for: map, stack, architecture, structure, conventions, testing, integrations, concerns
    - Show last built timestamp + validation status
  - **15) Context Packs Page (context/packs/**)**
    - List packs by task/phase + budget size
    - Actions: Build pack, Show pack, Diff pack since RUN
  - **16) State / Events / History Page (state/** + narrative history)**
    - state.json viewer (cursor, decisions, blockers, gating signals)
    - Events tail (events.ndjson) with filtering by type
    - "History summary" viewer (run/task keyed entries)
  - **17) Pause / Resume / Checkpoints Page**
    - Pause creates handoff.json snapshot
    - Resume validates alignment and continues
    - Checkpoint list/create/show/restore
    - Locks view + release (safety)
  - **18) Validation & Maintenance Page**
    - Buttons for: validate schemas/spec/state/evidence/codebase
    - "Repair indexes", cache clear/prune
    - Show validation report artifacts + links to failing files

- [x] **PH-PRD-0011 — Cross-Cutting UI Components**
  **Outcome:** shared components for consistent UX across all pages.
  - **Projects:** `Gmsd.Web`
  - **Code paths (target):**
    - `Gmsd.Web/Components/**`
  - **Workspace outputs (target):** *(none — UI only)*
  - **Verify:** components render consistently; keyboard shortcuts work

  - Global Command Palette (Ctrl+K): jump to any artifact / run / command
  - Persistent Workspace badge (current repo path + .aos health)
  - Unified Artifact link system (TSK/PH/MS/UAT/RUN clickthrough)
  - Toast/notification system: validation failures, run completion, lock conflicts

---

## MS-PRD-0009 — AOS Engine UI Business Logic

- [x] **PH-PRD-0012 — Workspace Management API**
  **Outcome:** backend services for workspace picker and configuration.
  - **Projects:** `Gmsd.Web`
  - **Code paths (target):**
    - `Gmsd.Web/Services/WorkspaceService.cs`
    - `Gmsd.Web/Models/WorkspaceModels.cs`
    - `Gmsd.Web/Controllers/WorkspaceController.cs`
  - **Workspace outputs (target):**
    - `.aos/config.json` (workspace path persistence)
  - **Verify:** can list, open, init, validate, repair workspaces via API

  - Workspace repository: CRUD for workspace records (path, name, last opened, health)
  - Validation service: check .aos presence, schema validity, locks
  - Init service: bootstrap .aos/ directory structure
  - Repair service: rebuild indexes, fix schema drift
  - Config integration: read/write aos config (workspace path, preferences)

- [ ] **PH-PRD-0013 — Dashboard & State API**
  **Outcome:** backend services for dashboard overview and cursor tracking.
  - **Projects:** `Gmsd.Web`
  - **Code paths (target):**
    - `Gmsd.Web/Services/DashboardService.cs`
    - `Gmsd.Web/Services/StateService.cs`
    - `Gmsd.Web/Controllers/DashboardController.cs`
  - **Workspace outputs (target):** *(read-only — no writes)*
  - **Verify:** dashboard shows correct cursor, blockers, next action

  - State reader: parse state.json → cursor (milestone/phase/task/step)
  - Blocker aggregator: collect open issues, locks, validation errors
  - Next action router: logic for plan-phase / execute-plan / verify-work / plan-fix / resume-work
  - Run summary service: latest run status, evidence links
  - Quick actions backend: validate, checkpoint, pause, resume, tail events

- [ ] **PH-PRD-0014 — Command Center API (Chat + Runs)**
  **Outcome:** backend services for chat UI and command execution.
  - **Projects:** `Gmsd.Web`
  - **Code paths (target):**
    - `Gmsd.Web/Services/CommandService.cs`
    - `Gmsd.Web/Services/ChatService.cs`
    - `Gmsd.Web/Controllers/CommandController.cs`
    - `Gmsd.Web/Hubs/CommandHub.cs` *(SignalR for streaming)*
  - **Workspace outputs (target):**
    - `.aos/evidence/runs/RUN-*/**`
  - **Verify:** commands execute; streaming output works; run summaries returned

  - Slash command router: map /init, /status, /validate, /spec, /run, /codebase, /pack, /checkpoint
  - Chat session manager: thread history, message persistence
  - Command execution wrapper: invoke Gmsd.Agents orchestrator, capture output
  - Streaming service: SignalR hub for real-time output (stdout/stderr)
  - Run summary builder: status, next action, evidence links from run results
  - Artifact attachment service: link evidence files, context packs

- [ ] **PH-PRD-0015 — Spec Management API**
  **Outcome:** backend services for spec CRUD, validation, and diff.
  - **Projects:** `Gmsd.Web`
  - **Code paths (target):**
    - `Gmsd.Web/Services/SpecService.cs`
    - `Gmsd.Web/Services/ValidationService.cs`
    - `Gmsd.Web/Controllers/SpecsController.cs`
  - **Workspace outputs (target):**
    - `.aos/spec/**` (read/write)
  - **Verify:** specs load, edit, validate; diff viewer works

  - Spec tree service: list all spec artifacts (project, roadmap, milestones, phases, tasks, issues, uat)
  - Spec CRUD: read/write JSON with schema validation
  - Form editor mapping: JSON ↔ form fields for interview mode
  - Validation service: schema validation, cross-reference checks
  - Diff service: compare spec versions (current vs previous)
  - Search service: full-text search across spec/

- [ ] **PH-PRD-0016 — Roadmap, Milestones, Phases API**
  **Outcome:** backend services for roadmap structure and phase planning.
  - **Projects:** `Gmsd.Web`
  - **Code paths (target):**
    - `Gmsd.Web/Services/RoadmapService.cs`
    - `Gmsd.Web/Services/MilestoneService.cs`
    - `Gmsd.Web/Services/PhaseService.cs`
    - `Gmsd.Web/Controllers/RoadmapController.cs`
  - **Workspace outputs (target):**
    - `.aos/spec/roadmap.json`
    - `.aos/spec/milestones/*.json`
    - `.aos/spec/phases/*.json`
  - **Verify:** can CRUD milestones/phases; reindexing works; planning generates tasks

  - Roadmap service: read/write roadmap.json, timeline ordering
  - Milestone service: CRUD milestones, completion gate logic
  - Phase service: CRUD phases, assumptions/research persistence
  - Phase planner: generate 2–3 atomic tasks + plans from phase goals
  - Alignment checker: verify roadmap.json ↔ state.json cursor consistency
  - Reindex service: auto renumber phases on add/remove

- [ ] **PH-PRD-0017 — Tasks, Runs, UAT, Issues API**
  **Outcome:** backend services for task execution and verification workflow.
  - **Projects:** `Gmsd.Web`
  - **Code paths (target):**
    - `Gmsd.Web/Services/TaskService.cs`
    - `Gmsd.Web/Services/RunService.cs`
    - `Gmsd.Web/Services/UatService.cs`
    - `Gmsd.Web/Services/IssueService.cs`
    - `Gmsd.Web/Controllers/TasksController.cs`, `RunsController.cs`, `UatController.cs`, `IssuesController.cs`
  - **Workspace outputs (target):**
    - `.aos/spec/tasks/**/*.json`
    - `.aos/evidence/runs/RUN-*/**`
    - `.aos/spec/uat/*.json`
    - `.aos/spec/issues/*.json`
  - **Verify:** task execution triggers runs; UAT wizard records results; issues linked correctly

  - Task service: CRUD tasks, filter by phase/milestone/status, status transitions
  - Task plan executor: execute plan.json steps via orchestrator
  - Run service: list runs, detail view (commands, logs, artifacts, files changed, commit hash)
  - UAT service: build checklist from task uat.json, record pass/fail + repro notes
  - Issue emitter: create issues from UAT failures, link to task/phase/run
  - Issue service: CRUD issues, filter by status/type/severity, resolution workflow

- [ ] **PH-PRD-0018 — Fix Planning, Intelligence, State Management API**
  **Outcome:** backend services for repair loop, codebase intelligence, and state control.
  - **Projects:** `Gmsd.Web`
  - **Code paths (target):**
    - `Gmsd.Web/Services/FixPlanningService.cs`
    - `Gmsd.Web/Services/CodebaseService.cs`
    - `Gmsd.Web/Services/ContextPackService.cs`
    - `Gmsd.Web/Services/CheckpointService.cs`
    - `Gmsd.Web/Services/PauseResumeService.cs`
  - **Workspace outputs (target):**
    - `.aos/handoff.json` (pause snapshot)
    - `.aos/checkpoints/**`
    - `.aos/context/packs/**`
    - `.aos/codebase/**`
  - **Verify:** fix plans generate; codebase scans trigger; checkpoints create/restore; pause/resume works

  - Fix planning service: generate fix tasks from issues (max small set)
  - Fix loop manager: execute fix, re-verify, clear loop state
  - Codebase scanner: trigger map/symbols/graph builds
  - Intelligence viewer service: retrieve map, stack, architecture, structure, conventions
  - Context pack service: build packs, list by task/phase, diff since RUN
  - State viewer service: parse state.json for cursor, decisions, blockers, gating signals
  - Events tail service: stream events.ndjson with filtering
  - Checkpoint service: create/show/restore checkpoints
  - Pause service: create handoff.json snapshot
  - Resume service: validate alignment, continue from snapshot
  - Lock service: view/release locks (safety)

- [ ] **PH-PRD-0019 — Validation & Maintenance API**
  **Outcome:** backend services for system validation and maintenance.
  - **Projects:** `Gmsd.Web`
  - **Code paths (target):**
    - `Gmsd.Web/Services/SystemValidationService.cs`
    - `Gmsd.Web/Services/MaintenanceService.cs`
    - `Gmsd.Web/Controllers/ValidationController.cs`
  - **Workspace outputs (target):**
    - `.aos/validation-reports/*.json`
  - **Verify:** all validation buttons work; repair actions execute; cache clears

  - Validation orchestrator: run schema/spec/state/evidence/codebase validations
  - Report generator: validation report artifacts with failing file links
  - Repair service: repair indexes, rebuild caches
  - Cache management: clear/prune caches
  - System health aggregator: overall .aos/ health check

---

# 4) HOST ROADMAP (Windows Service — Optional/Production)

> Goal: provide a Windows Service host for production deployments. Skip this for MVP/debugging — use direct invocation in `Gmsd.Web` instead.

---

## MS-HST-0001 — Windows Service Host

- [ ] **PH-HST-0001 — Windows Service Host (`Gmsd.Windows.Service`)**  
  **Outcome:** daemon that runs agent runtime with shared configuration and filesystem access.
  - **Projects:** `Gmsd.Windows.Service`
  - **Code paths (target):**
    - `Gmsd.Windows.Service/Program.cs`
    - `Gmsd.Windows.Service/appsettings*.json`
    - `Gmsd.Agents/Workers/**`
    - `Gmsd.Windows.Service/**` (service hosting + lifecycle)
  - **Workspace outputs (target):** *(none directly — the host runs workflows that write `.aos/**` in the target workspace)*  
  - **Example artifacts:** *(n/a — hosting only)*
  - **Verify:** service starts in console mode; can execute a no-op command against a test workspace
  - Host wiring + DI  
    - **Scope:** `Gmsd.Windows.Service/Program.cs`, `Gmsd.Windows.Service/appsettings*.json`  
    - **Outputs:** loads `AddGmsdAgents(...)`, binds config, logging  
    - **Verify:** service starts (console run ok)
  - Background worker execution model  
    - **Scope:** `Gmsd.Agents/Workers/**`, `Gmsd.Windows.Service/**`  
    - **Outputs:** worker accepts run requests and executes orchestrator  
    - **Verify:** executes no-op command in test workspace

- [ ] **PH-HST-0002 — Windows Service API (`Gmsd.Windows.Service.Api`)**  
  **Outcome:** stable API surface to start/monitor/control runs (no engine leakage).
  - **Projects:** `Gmsd.Windows.Service.Api`
  - **Code paths (target):**
    - `Gmsd.Windows.Service.Api/v1/Controllers/**`
    - `Gmsd.Windows.Service.Api/**` (API composition, hosting, auth, clients)
    - `Gmsd.Agents/Execution/Orchestrator/**` (invoked by the API)
  - **Workspace outputs (target):**
    ```text
    .aos/
      evidence/
        runs/
          RUN-*/  (created in the workspace targeted by a run request)
    ```
  - **Example artifacts:**
    - `.aos/evidence/runs/RUN-*/summary.json`
    - `.aos/evidence/runs/RUN-*/commands.json`
  - **Verify:** swagger loads; sample command returns normalized response; runs create `.aos/evidence/runs/RUN-*` under the requested workspace root
  - Controllers (Commands/Runs/Service/Health)  
    - **Scope:** `Gmsd.Windows.Service.Api/v1/Controllers/**`  
    - **Outputs:** endpoints to submit commands, query runs, manage service state  
    - **Verify:** swagger loads; sample command returns normalized response
  - API ↔ Orchestrator integration  
    - **Scope:** `Gmsd.Windows.Service.Api/**`, `Gmsd.Agents/Execution/Orchestrator/**`  
    - **Outputs:** API calls orchestrator with strict workspace root parameter  
    - **Verify:** runs produce `.aos/evidence/runs/RUN-*`

---

## MS-HST-0002 — Product ↔ Service Integration (Production Mode)

- [ ] **PH-HST-0003 — UI can start runs via Service API**  
  **Outcome:** product integrates with Windows Service for production deployments.
  - **Projects:** `Gmsd.Web`, `Gmsd.Windows.Service.Api`
  - **Code paths (target):**
    - `Gmsd.Web/Clients/**` *(typed client wrapper around Service Manager API)*
    - `Gmsd.Web/Configuration/**` *(service vs direct mode toggle)*
    - `Gmsd.Windows.Service.Api/**` *(auth boundary + endpoints)*
  - **Workspace outputs (target):** *(no `.aos/**` writes from Product directly)*
  - **Example artifacts:**
    - `Gmsd.Web/Clients/ServiceManagerApiClient.cs`
    - `Gmsd.Web/Configuration/AgentRunnerMode.cs`
  - **Verify:** UI calls Service API Health and can submit a no-op command; unauthorized run-control calls are rejected
  - Service Manager API client  
    - **Scope:** `Gmsd.Web/Clients/**`  
    - **Outputs:** typed HTTP client for `Gmsd.Windows.Service.Api` endpoints  
    - **Verify:** calls Health + submits a no-op command
  - Mode toggle (Direct vs Service)  
    - **Scope:** `Gmsd.Web/Configuration/**`  
    - **Outputs:** config switch to select direct runner (MVP/debug) vs service client (production)  
    - **Verify:** toggles without code changes
  - Minimal auth boundary  
    - **Scope:** `Gmsd.Windows.Service.Api/**`  
    - **Outputs:** API key or auth to protect run controls  
    - **Verify:** unauthorized calls rejected

---

## Optional later milestones (once core loop is proven)
- Import/Export + workspace portability
- Advanced roadmap governance (reorder/branch)
- Full Help/Usage surfaced in Product UI


