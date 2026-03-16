# Change: Scaffold nirmata.Agents DI + Configuration Baseline

## Why
`nirmata.Agents` (the Plane layer) needs a unified composition root so hosts can wire up workflows consistently via one extension method. Currently, only LLM provider configuration exists; we need the full DI surface including runtime models, persistence abstractions, and observability scaffolding. This enables the Plane to consume Engine services directly (not via CLI) for richer error handling and better observability.

## What Changes
- Add `AddnirmataAgents()` composition root in `nirmata.Agents/Composition/`
- Add `AgentsOptions` configuration class for Plane-specific settings
- Add runtime models for run request/response in `nirmata.Agents/Models/`
- Add persistence abstractions (wrappers over Engine stores) in `nirmata.Agents/Persistence/`
- Add observability scaffolding (correlation ID = RUN-*) in `nirmata.Agents/Observability/`
- Add `appsettings.json` schema documentation for Agents configuration section

## Impact
- **Affected specs:** New `agents-composition-root` capability; touches existing `agents-llm-provider-abstraction` configuration patterns
- **Affected code:** `nirmata.Agents/Composition/**`, `nirmata.Agents/Configuration/**`, `nirmata.Agents/Models/**`, `nirmata.Agents/Persistence/**`, `nirmata.Agents/Observability/**`
- **Dependencies:** Consumes `nirmata.Aos` services via `AddnirmataAos()`
