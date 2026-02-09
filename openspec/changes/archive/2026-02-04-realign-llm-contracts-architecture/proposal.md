# Change: Realign LLM contracts with Agent Plane architecture

## Why
LLM contracts, adapters, and provider abstractions are currently in `Gmsd.Aos` but per the architecture design (PDF diagram and clarified separation of concerns), they belong in `Gmsd.Agents`. The AOS engine provides workspace services, validation, state management, and evidence capture - but does not orchestrate LLM calls. Workflows in Gmsd.Agents orchestrate LLM interactions and should own the contracts.

Current state creates architectural confusion:
- `Gmsd.Aos/Contracts/Llm/` contains `ILlmProvider`, message types, tool definitions
- `Gmsd.Aos/Adapters/` contains provider implementations (OpenAI, Anthropic, etc.)
- `Gmsd.Agents` is nearly empty but should contain the orchestration plane

## What Changes
- **BREAKING**: Move `Gmsd.Aos/Contracts/Llm/` → `Gmsd.Agents/Contracts/Llm/`
- **BREAKING**: Move `Gmsd.Aos/Adapters/` → `Gmsd.Agents/Adapters/`
- **BREAKING**: Move `Gmsd.Aos/Configuration/Llm*.cs` → `Gmsd.Agents/Configuration/`
- **BREAKING**: Move embedded prompt resources from `Gmsd.Aos/Resources/Prompts/` → `Gmsd.Agents/Resources/Prompts/`
- Update `Gmsd.Agents.csproj` to reference required packages (currently missing)
- Add `Gmsd.Aos` reference to `Gmsd.Agents` so Agents can call AOS services
- Rename spec `aos-llm-provider-abstraction` → `agents-llm-provider-abstraction`
- Update `openspec/roadmap.md` to reflect corrected project structure

## Impact
- **Affected specs**: `aos-llm-provider-abstraction` (renamed → `agents-llm-provider-abstraction`)
- **Affected code**: 
  - `Gmsd.Aos/Contracts/Llm/*.cs` → `Gmsd.Agents/Contracts/Llm/*.cs`
  - `Gmsd.Aos/Adapters/**/*.cs` → `Gmsd.Agents/Adapters/**/*.cs`
  - `Gmsd.Aos/Configuration/Llm*.cs` → `Gmsd.Agents/Configuration/Llm*.cs`
  - `Gmsd.Aos/Resources/Prompts/` → `Gmsd.Agents/Resources/Prompts/`
  - `Gmsd.Aos/Gmsd.Aos.csproj` (remove LLM-related packages)
  - `Gmsd.Agents/Gmsd.Agents.csproj` (add packages + Aos reference)
- **Breaking changes**: All imports of LLM contracts will change namespace/project
- **Migration path**: Update using statements from `Gmsd.Aos.Contracts.Llm` → `Gmsd.Agents.Contracts.Llm`

## Dependencies
None - this is a structural reorganization with no new dependencies.

## Risks
- Test files in `Gmsd.Aos.Tests` that reference LLM contracts will need updates
- Any code using LLM types directly will need namespace updates
- Risk of missing references during the move
