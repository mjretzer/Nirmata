# Change: Realign LLM contracts with Agent Plane architecture

## Why
LLM contracts, adapters, and provider abstractions are currently in `nirmata.Aos` but per the architecture design (PDF diagram and clarified separation of concerns), they belong in `nirmata.Agents`. The AOS engine provides workspace services, validation, state management, and evidence capture - but does not orchestrate LLM calls. Workflows in nirmata.Agents orchestrate LLM interactions and should own the contracts.

Current state creates architectural confusion:
- `nirmata.Aos/Contracts/Llm/` contains `ILlmProvider`, message types, tool definitions
- `nirmata.Aos/Adapters/` contains provider implementations (OpenAI, Anthropic, etc.)
- `nirmata.Agents` is nearly empty but should contain the orchestration plane

## What Changes
- **BREAKING**: Move `nirmata.Aos/Contracts/Llm/` → `nirmata.Agents/Contracts/Llm/`
- **BREAKING**: Move `nirmata.Aos/Adapters/` → `nirmata.Agents/Adapters/`
- **BREAKING**: Move `nirmata.Aos/Configuration/Llm*.cs` → `nirmata.Agents/Configuration/`
- **BREAKING**: Move embedded prompt resources from `nirmata.Aos/Resources/Prompts/` → `nirmata.Agents/Resources/Prompts/`
- Update `nirmata.Agents.csproj` to reference required packages (currently missing)
- Add `nirmata.Aos` reference to `nirmata.Agents` so Agents can call AOS services
- Rename spec `aos-llm-provider-abstraction` → `agents-llm-provider-abstraction`
- Update `openspec/roadmap.md` to reflect corrected project structure

## Impact
- **Affected specs**: `aos-llm-provider-abstraction` (renamed → `agents-llm-provider-abstraction`)
- **Affected code**: 
  - `nirmata.Aos/Contracts/Llm/*.cs` → `nirmata.Agents/Contracts/Llm/*.cs`
  - `nirmata.Aos/Adapters/**/*.cs` → `nirmata.Agents/Adapters/**/*.cs`
  - `nirmata.Aos/Configuration/Llm*.cs` → `nirmata.Agents/Configuration/Llm*.cs`
  - `nirmata.Aos/Resources/Prompts/` → `nirmata.Agents/Resources/Prompts/`
  - `nirmata.Aos/nirmata.Aos.csproj` (remove LLM-related packages)
  - `nirmata.Agents/nirmata.Agents.csproj` (add packages + Aos reference)
- **Breaking changes**: All imports of LLM contracts will change namespace/project
- **Migration path**: Update using statements from `nirmata.Aos.Contracts.Llm` → `nirmata.Agents.Contracts.Llm`

## Dependencies
None - this is a structural reorganization with no new dependencies.

## Risks
- Test files in `nirmata.Aos.Tests` that reference LLM contracts will need updates
- Any code using LLM types directly will need namespace updates
- Risk of missing references during the move
