## Context
The project has LLM provider contracts, adapters, and configuration in `Gmsd.Aos`, but the architecture diagram (PDF) and clarified separation of concerns place these in `Gmsd.Agents`. 

**Current confusion:**
- Gmsd.Aos is meant to be the supporting engine (workspace, validation, state, evidence)
- Gmsd.Agents is meant to be the orchestration plane (workflows that call LLMs)
- LLM contracts being in Aos suggests Aos orchestrates LLM calls - but it doesn't

**Clarified flow:**
```
User Prompt → Agent Workflow → Calls LLM (via contracts/adapters in Agents)
                      ↓
              Calls AOS Services (validate, state, evidence)
```

## Goals / Non-Goals

### Goals
- Realign code structure with architectural intent (PDF diagram)
- Make Gmsd.Agents the owner of LLM orchestration concerns
- Make Gmsd.Aos LLM-agnostic (only knows about evidence envelopes, not LLM types)
- Establish clear dependency direction: Agents → Aos (not bidirectional)

### Non-Goals
- Changing the actual LLM contract interfaces (types remain the same)
- Changing provider adapter implementations (just moving them)
- Adding new LLM features or providers
- Changing how evidence capture works (LlmCallEnvelope stays, just namespace update)

## Decisions

### Decision 1: Move entire LLM stack to Agents
**What:** Contracts, adapters, configuration, and prompt resources all move to Gmsd.Agents
**Why:** Workflows in Agents are the orchestrators. They decide when to call LLMs, which provider to use, and how to handle responses. AOS just captures evidence of what happened.

**Alternatives considered:**
- Keep contracts in Aos, only move adapters → Rejected: creates split ownership, Agents would need to reference Aos for basic LLM types
- Create separate `Gmsd.Llm` library → Rejected: overkill for current scope, Agents are the primary (only) consumer

### Decision 2: AOS stays LLM-agnostic
**What:** AOS captures `LlmCallEnvelope` but doesn't import LLM contract types directly
**Why:** AOS operates on evidence, not on LLM semantics. The envelope is a serialization format, not a contract dependency.

**Migration approach:**
- `LlmCallEnvelope` stays in Aos but uses primitive types (strings) for provider/model references
- Or: Aos defines a minimal `ILlmCallInfo` interface that Agents implement
- Preferred: Keep envelope as POCO with string properties, no reference to Gmsd.Agents

### Decision 3: Dependency direction
**What:** Gmsd.Agents references Gmsd.Aos; Gmsd.Aos does NOT reference Gmsd.Agents
**Why:** Agents orchestrate and use AOS services. AOS provides workspace services without knowing about the orchestration layer.

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Breaking all LLM-related code | Staged move: contracts first, then adapters, then config; compile after each stage |
| Test files in wrong project | Audit Gmsd.Aos.Tests before move; move LLM tests to new Gmsd.Agents.Tests |
| Missing package references in Agents.csproj | Review Aos.csproj package list; copy relevant ones to Agents |
| Embedded resources not loading | Verify resource path in Agents.csproj matches namespace |

## Migration Plan

### Phase 1: Contracts (no dependencies)
1. Move `Contracts/Llm/*.cs` to Agents
2. Update namespaces
3. Verify compiles

### Phase 2: Configuration (depends on contracts)
1. Move `Configuration/Llm*.cs` to Agents  
2. Update namespaces
3. Verify compiles

### Phase 3: Adapters (depends on contracts + config)
1. Move `Adapters/**/*.cs` to Agents
2. Update namespaces and usings
3. Verify compiles

### Phase 4: Resources (depends on nothing)
1. Move `Resources/Prompts/` to Agents
2. Update .csproj files
3. Verify embedded resources load

### Phase 5: AOS cleanup
1. Remove LLM packages from Aos.csproj if unused
2. Update `LlmCallEnvelope` to not reference LLM types (use strings)
3. Verify Aos compiles without LLM knowledge

### Phase 6: Tests
1. Move/adjust test files
2. Run full test suite

### Rollback
If issues arise during any phase:
- Git revert the change
- Fix forward on a branch
- Re-apply when validated

## Open Questions
None - architecture is clarified and approved.
