## MODIFIED (project.md)

### Engine (AOS) concerns - LLM abstractions removed

The Engine (AOS) concerns section in `openspec/project.md` SHALL be updated to remove LLM provider abstractions from the AOS scope.

Updated AOS concerns:
- Deterministic workflow routing & gating (spec-first)
- AOS workspace file contracts (`.aos/spec`, `.aos/state`, `.aos/evidence`, `.aos/context`, `.aos/codebase`, `.aos/cache`)
- Planning & execution workflows + verification/fix loop
- Run lifecycle + resumability (pause/resume)
- ~~Tools abstraction (LLM providers, MCP tools, filesystem/process/git tools)~~ **MOVED TO AGENTS**
- Tool execution framework (ITool contracts, registry, invocation)
- Schema validation + invariants
- **Evidence capture for LLM calls** (record envelopes via IAosEvidenceWriter, but LLM types are opaque strings)

#### Scenario: AOS remains LLM-agnostic
- **GIVEN** the AOS engine provides workspace services
- **WHEN** an Agent workflow performs an LLM call
- **THEN** AOS captures evidence using primitive types (strings) without importing LLM contract types
- **AND** AOS does not have a project reference to Gmsd.Agents

### Agent Plane (Gmsd.Agents) concerns - LLM abstractions added

The Agent Plane (Gmsd.Agents) section in `openspec/project.md` SHALL be updated to include LLM provider abstractions.

Updated Agents concerns:
- Workflow implementations (planner/executor/verifier/fix, continuity, brownfield mapping, etc.)
- Composes workflows against AOS contracts/tools
- **LLM provider abstractions** (ILlmProvider, message types, tool definitions)
- **Provider adapters** (OpenAI, Anthropic, Azure OpenAI, Ollama)
- **Prompt template loading**
- **MCP tool integration**

#### Scenario: Agents orchestrate LLM calls
- **GIVEN** an Agent workflow needs LLM completion
- **WHEN** the workflow uses ILlmProvider from Gmsd.Agents.Contracts.Llm
- **THEN** the call is made through the appropriate provider adapter
- **AND** evidence is recorded to AOS via IAosEvidenceWriter using primitive types

### Dependency direction clarified

The Dependency direction section SHALL be updated to reflect that Gmsd.Agents references Gmsd.Aos (not vice versa), and Gmsd.Aos is LLM-agnostic.

Updated allowed references:
- `Gmsd.Aos` → `Gmsd.Common` (and internal engine dependencies only)
- `Gmsd.Agents` → `Gmsd.Aos`, `Gmsd.Common`
- Hosts (`Gmsd.Windows.Service*`) → `Gmsd.Agents`, `Gmsd.Aos`, `Gmsd.Common`

Hard boundaries remain:
- Engine projects must not reference Product Application projects
- **NEW**: AOS must not reference Gmsd.Agents (prevents circular dependency)
- AOS operates on evidence strings, not LLM types

#### Scenario: Agents depends on AOS for workspace services
- **GIVEN** Gmsd.Agents implements an LLM-calling workflow
- **WHEN** the workflow needs to validate state or record evidence
- **THEN** Agents calls AOS services via established interfaces
- **AND** AOS has no knowledge of the LLM types being used
