# Change: Scaffold Gmsd.Agents DI + Configuration Baseline

## Why
`Gmsd.Agents` (the Plane layer) needs a unified composition root so hosts can wire up workflows consistently via one extension method. Currently, only LLM provider configuration exists; we need the full DI surface including runtime models, persistence abstractions, and observability scaffolding. This enables the Plane to consume Engine services directly (not via CLI) for richer error handling and better observability.

## What Changes
- Add `AddGmsdAgents()` composition root in `Gmsd.Agents/Composition/`
- Add `AgentsOptions` configuration class for Plane-specific settings
- Add runtime models for run request/response in `Gmsd.Agents/Models/`
- Add persistence abstractions (wrappers over Engine stores) in `Gmsd.Agents/Persistence/`
- Add observability scaffolding (correlation ID = RUN-*) in `Gmsd.Agents/Observability/`
- Add `appsettings.json` schema documentation for Agents configuration section

## Impact
- **Affected specs:** New `agents-composition-root` capability; touches existing `agents-llm-provider-abstraction` configuration patterns
- **Affected code:** `Gmsd.Agents/Composition/**`, `Gmsd.Agents/Configuration/**`, `Gmsd.Agents/Models/**`, `Gmsd.Agents/Persistence/**`, `Gmsd.Agents/Observability/**`
- **Dependencies:** Consumes `Gmsd.Aos` services via `AddGmsdAos()`
