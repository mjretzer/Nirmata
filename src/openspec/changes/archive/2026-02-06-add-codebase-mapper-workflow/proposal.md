# Change: Add Codebase Mapper Workflow

## Why
The engine needs repository intelligence to enable grounded, context-aware planning and execution. Currently, there is no structured mechanism to capture codebase structure, symbol relationships, file dependencies, and architectural conventions. The Codebase Mapper workflow will produce an `.aos/codebase/**` intelligence pack that serves as the foundation for brownfield planning.

## What Changes
- **ADDED** New capability `codebase-mapping` with core components:
  - `CodebaseScanner` — scans repository structure and builds codebase intelligence
  - `SymbolCacheBuilder` — derives symbol indices for fast lookup
  - `FileGraphBuilder` — derives file dependency graph for relationship mapping
  - `MapValidator` — validates codebase map integrity and determinism
- **ADDED** Workspace file contracts for codebase intelligence outputs
  - `map.json` — high-level codebase overview
  - `stack.json` — technology stack detection
  - `architecture.json` — architectural patterns and boundaries
  - `structure.json` — directory/file structure with metadata
  - `conventions.json` — coding conventions and patterns
  - `testing.json` — test structure and coverage mapping
  - `integrations.json` — external integration points
  - `concerns.json` — cross-cutting concerns and hotspots
  - `cache/symbols.json` — symbol index cache
  - `cache/file-graph.json` — file dependency graph cache
- **ADDED** Deterministic JSON serialization for all codebase artifacts
- **Impact on nirmata.Agents**: New `Execution/Brownfield/CodebaseMapper/**` directory structure

## Impact
- **Affected specs:** NEW capability `codebase-mapping`
- **Affected code:** 
  - `nirmata.Agents/Execution/Brownfield/CodebaseMapper/CodebaseScanner/**`
  - `nirmata.Agents/Execution/Brownfield/CodebaseMapper/SymbolCacheBuilder/**`
  - `nirmata.Agents/Execution/Brownfield/CodebaseMapper/FileGraphBuilder/**`
  - `nirmata.Agents/Execution/Brownfield/CodebaseMapper/MapValidator/**`
- **Workspace outputs:** `.aos/codebase/**` with all intelligence pack files

## Success Criteria
1. Codebase scan produces valid, complete intelligence pack for any .NET solution
2. Symbol cache is deterministic for the same repo state
3. File graph correctly captures project-level dependencies
4. Validation passes for all generated artifacts
5. Rebuild produces byte-identical output for unchanged repository

## Sequencing
This change is foundational for brownfield planning. It should be completed before implementing phase-level brownfield analysis workflows.
