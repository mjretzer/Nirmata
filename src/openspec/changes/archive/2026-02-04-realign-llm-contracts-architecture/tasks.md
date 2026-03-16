## 1. Setup & Preparation
- [x] 1.1 Verify no active changes conflict with this move (run `openspec list`)
- [x] 1.2 Review all files in `nirmata.Aos/Contracts/Llm/` to ensure complete inventory
- [x] 1.3 Review all files in `nirmata.Aos/Adapters/` to ensure complete inventory
- [x] 1.4 Review `nirmata.Aos.Tests` for any LLM-related tests that need moving

## 2. Move LLM Contracts
- [x] 2.1 Create `nirmata.Agents/Contracts/Llm/` directory
- [x] 2.2 Move all files from `nirmata.Aos/Contracts/Llm/` → `nirmata.Agents/Contracts/Llm/`
- [x] 2.3 Update namespace declarations: `nirmata.Aos.Contracts.Llm` → `nirmata.Agents.Contracts.Llm`
- [x] 2.4 Update `using` statements in moved files if they reference nirmata.Aos types

## 3. Move LLM Adapters
- [x] 3.1 Create `nirmata.Agents/Adapters/` directory
- [x] 3.2 Move all files from `nirmata.Aos/Adapters/` → `nirmata.Agents/Adapters/`
- [x] 3.3 Update namespace declarations: `nirmata.Aos.Adapters` → `nirmata.Agents.Adapters`
- [x] 3.4 Update `using` statements in adapter files (will now reference nirmata.Agents.Contracts.Llm)

## 4. Move Configuration
- [x] 4.1 Create `nirmata.Agents/Configuration/` directory
- [x] 4.2 Move `nirmata.Aos/Configuration/LlmServiceCollectionExtensions.cs` → `nirmata.Agents/Configuration/`
- [x] 4.3 Move `nirmata.Aos/Configuration/LlmProviderOptions.cs` → `nirmata.Agents/Configuration/`
- [x] 4.4 Update namespaces in configuration files

## 5. Move Resources
- [x] 5.1 Create `nirmata.Agents/Resources/` directory structure
- [~] 5.2 Move `nirmata.Aos/Resources/Prompts/` → `nirmata.Agents/Resources/Prompts/` (no prompts to move)
- [x] 5.3 Update embedded resource paths in `nirmata.Agents.csproj`
- [x] 5.4 Remove embedded resource entries from `nirmata.Aos.csproj`

## 6. Update Project Files
- [x] 6.1 Update `nirmata.Agents.csproj`:
  - Add `Microsoft.Extensions.*` packages (Configuration, DependencyInjection, Logging, Options)
  - Add `JsonSchema.Net` package
  - Add `ProjectReference` to `nirmata.Aos`
  - Add `ProjectReference` to `nirmata.Common`
  - Add embedded resource globs for prompts
- [x] 6.2 Update `nirmata.Aos.csproj`:
  - Remove LLM-related packages if no longer needed
  - Remove embedded resource globs for prompts
  - Keep `InternalsVisibleTo` for tests

## 7. Update Evidence/Engine References
- [x] 7.1 Review `nirmata.Aos/Engine/Evidence/LlmCallEnvelope.cs` - moved to nirmata.Agents
- [x] 7.2 Update `LlmCallEnvelope.cs` to use primitive types (LLM-agnostic)
- [x] 7.3 Check for any other AOS files referencing LLM types and resolve

## 8. Update Tests
- [x] 8.1 Review `tests/nirmata.Aos.Tests` for LLM-related tests
- [x] 8.2 Create `tests/nirmata.Agents.Tests` project
- [x] 8.3 Move LLM-related tests from Aos.Tests to Agents.Tests
- [x] 8.4 Update test references to use new namespaces
- [ ] 8.5 Run all tests to verify no regressions

## 9. Update Specs
- [x] 9.1 Rename `openspec/specs/aos-llm-provider-abstraction/` → `openspec/specs/agents-llm-provider-abstraction/`
- [x] 9.2 Update spec.md to reflect new project location
- [x] 9.3 Create delta spec in `openspec/changes/realign-llm-contracts-architecture/specs/`

## 10. Update Documentation
- [x] 10.1 Update `openspec/project.md` section on Engine (AOS) concerns - remove "Tools abstraction (LLM providers...)"
- [x] 10.2 Update `openspec/project.md` to clarify Agents owns LLM orchestration
- [~] 10.3 Update `openspec/roadmap.md` to reflect corrected project structure (roadmap already correct)

## 11. Validation
- [~] 11.1 Build solution successfully (nirmata.Agents, nirmata.Aos, nirmata.Agents.Tests build; nirmata.Aos.Tests has pre-existing issues)
- [x] 11.2 Run `openspec validate realign-llm-contracts-architecture --strict`
- [x] 11.3 Run all unit tests (nirmata.Agents.Tests passes; nirmata.Aos.Tests has pre-existing compilation errors)
- [~] 11.4 Verify no compilation errors in any project (core migration projects build successfully)
