# Developer Quick-Start & Common Patterns

This document is a practical cheat sheet for working on the Nirmata repo using the AOS engine.

---

## Starting a New Feature / Fix (Existing Workspace)

```bash
# Check where you are
aos status

# If you're mid-phase and just resumed:
resume-work

# If you need to add urgent new work to the roadmap:
insert-phase <PH-INDEX>          # before a specific phase
# OR
add-phase                        # append to end
```

---

## Planning a Phase Before You Code

```bash
# 1. Surface assumptions early (optional but recommended)
list-phase-assumptions PH-####

# 2. Align on constraints before planning
discuss-phase PH-####

# 3. Research niche/uncertain areas (optional)
research-phase PH-####

# 4. Generate the task plan (2–3 atomic tasks)
plan-phase PH-####
```

---

## Executing a Plan

```bash
# Start execution (engine runs each task sequentially, fresh subagent per task)
execute-plan

# If interrupted mid-task:
pause-work                        # saves handoff.json

# Resume later:
resume-work                       # restores from handoff.json
# OR resume a specific run:
resume-task <EXECUTION-ID>        # RUN-* id from .aos/evidence/runs/
```

---

## Verifying & Fixing

```bash
# After execution completes, run formal verification
verify-work PH-####

# If pass: proceed to next phase
# If fail: fix planner is auto-invoked, or explicitly:
plan-fix                          # reads UAT artifacts + issues, writes fix task plans
execute-plan                      # execute fix tasks
verify-work PH-####               # re-verify same checks
```

---

## Capturing Work Without Interrupting Flow

```bash
# Capture an idea mid-flight
add-todo "add retry logic to Task Executor on transient LLM failures"

# Later, review and route it
check-todos [area]                # optional area filter
# → select a todo → converts to task or phase insertion
```

---

## Common File Locations for Debugging

| What You're Looking For | Where to Find It |
|---|---|
| Current cursor / where you are | `.aos/state/state.json` → `position` |
| What happened in the last run | `.aos/evidence/runs/RUN-*/summary.json` |
| Why a task failed | `.aos/evidence/runs/RUN-*/logs/` |
| The plan for a task | `.aos/spec/tasks/TSK-######/plan.json` |
| UAT findings from last verify | `.aos/spec/uat/UAT-*.json` or `.aos/spec/tasks/TSK-*/uat.json` |
| Open issues | `.aos/spec/issues/ISS-*.json` |
| Decisions made | `.aos/state/state.json` → `decisions[]` |
| Blockers | `.aos/state/state.json` → `blockers[]` |
| Full event history | `.aos/state/events.ndjson` |
| Codebase conventions | `.aos/codebase/conventions.json` |
| Repo architecture map | `.aos/codebase/architecture.json` |

---

## Working Rules for AI Assistants on This Repo

These rules apply when implementing tasks from a `plan.json`:

1. **Minimal, surgical changes** — only touch `allowedFiles` from the task plan.
2. **Follow existing patterns** — find the nearest similar module and match it.
3. **No comment/doc changes** unless explicitly requested.
4. **JSON contracts are stable** — avoid breaking changes; version or make backward-compatible if you must change.
5. **Fix root causes, not symptoms** — trace failures to their source.
6. **Add targeted tests** — cover your specific change, not unrelated code.
7. **Verify with the plan's `verificationCommands`** — don't declare done without running them.
8. **If scope needs to expand**, emit a scope-expansion signal — do not silently edit outside `allowedFiles`.

---

## Repo Structure Quick Reference

```
Gmsd.sln
├── src/
│   ├── Gmsd.Agents/          ← Core agent/orchestration (main working area)
│   │   ├── Engine/           ← Workspace, paths, serialization, validation, state, evidence
│   │   ├── Workflows/        ← Agent workflow implementations
│   │   ├── Llm/              ← LLM provider adapters (OpenAI, Anthropic, Ollama, Azure)
│   │   ├── Tools/            ← Tool implementations (files, process, git, MCP, AOS)
│   │   └── Prompts/          ← Prompt templates
│   ├── Gmsd.Aos/             ← AOS workspace library (spec/state/evidence management)
│   ├── Gmsd.Windows.Service/ ← Windows Service daemon host
│   ├── Gmsd.Windows.Service.Api/ ← Agent Manager HTTP API (commands, runs, service)
│   ├── Gmsd.Api/             ← Product REST API (Controllers/v1/)
│   ├── Gmsd.Services/        ← Product business logic
│   ├── Gmsd.Data/            ← EF Core + migrations
│   ├── Gmsd.Data.Dto/        ← DTOs + validators
│   ├── Gmsd.Common/          ← Shared utilities
│   └── Gmsd.Web/             ← Razor Pages web UI
├── nirmata.frontend/         ← React / Vite / TypeScript frontend (shadcn/ui)
└── tests/                    ← Test projects
```

---

## Build & Test Commands

```bash
# Build entire solution
dotnet build Gmsd.sln

# Build specific project
dotnet build src/Gmsd.Agents/Gmsd.Agents.csproj

# Run all tests
dotnet test tests/

# Run targeted tests (preferred for task verification)
dotnet test tests/Gmsd.Agents.Tests/ --filter Category=<AgentName>

# Validate AOS workspace
aos validate
aos validate spec
aos validate state
```

---

## LLM Provider Configuration

Adapters live in `Gmsd.Agents/Llm/Adapters/`:

| Provider | Adapter Directory |
|---|---|
| OpenAI | `Adapters/OpenAi/` |
| Anthropic | `Adapters/Anthropic/` |
| Azure OpenAI | `Adapters/AzureOpenAi/` |
| Ollama | `Adapters/Ollama/` |

Configured via `appsettings.json` in the host project (`Gmsd.Windows.Service/` or `Gmsd.Windows.Service.Api/`).

---

## Tool Registry

Tools are registered via `Tools/Registry/` and invoked by the engine at runtime. Available tool categories:

| Category | Directory | Examples |
|---|---|---|
| AOS artifacts | `Tools/Aos/` | Read/write spec, state, evidence, validate |
| Filesystem | `Tools/Files/` | Read file, write file, list directory |
| Process | `Tools/Process/` | Run dotnet build/test, capture output |
| Git | `Tools/Git/` | Stage files, commit, status |
| MCP | `Tools/Mcp/` | Any MCP server turned into ITool |

---

## Prompt Templates

Prompt assets live in `Gmsd.Agents/Prompts/Templates/`:

```
Templates/
  system.core.md          ← Core system prompt shared across all workflows
  workflows/              ← Per-workflow-specific prompt templates
```

Prompt wording is kept **separate from engine code** so it can evolve without changing workflow logic.
