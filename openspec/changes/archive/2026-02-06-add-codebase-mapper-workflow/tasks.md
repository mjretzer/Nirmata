# Implementation Tasks: Add Codebase Mapper Workflow

## 1. Workspace Contracts & Schemas
- [x] 1.1 Define `map.json` schema (version, repository, scanTimestamp, summary)
- [x] 1.2 Define `stack.json` schema (languages, frameworks, buildTools, packageManagers)
- [x] 1.3 Define `architecture.json` schema (layers, boundaries, patterns, entryPoints)
- [x] 1.4 Define `structure.json` schema (directories, files, metadata, statistics)
- [x] 1.5 Define `conventions.json` schema (naming, organization, stylePatterns)
- [x] 1.6 Define `testing.json` schema (testProjects, coverage, frameworks)
- [x] 1.7 Define `integrations.json` schema (externalApis, databases, services)
- [x] 1.8 Define `concerns.json` schema (crossCutting, hotspots, complexity)
- [x] 1.9 Define `cache/symbols.json` schema (symbols, locations, references)
- [x] 1.10 Define `cache/file-graph.json` schema (nodes, edges, weights)
- [x] 1.11 Add all schema constants to `Gmsd.Aos` schema registry

## 2. CodebaseScanner
- [x] 2.1 Create `ICodebaseScanner` interface
- [x] 2.2 Implement repository root detection
- [x] 2.3 Implement solution/project discovery
- [x] 2.4 Implement file classification (code, test, config, etc.)
- [x] 2.5 Implement technology stack detection
- [x] 2.6 Implement directory structure mapping
- [x] 2.7 Write all intelligence pack JSON files
- [x] 2.8 Unit tests: scan produces valid outputs for sample solution

## 3. SymbolCacheBuilder
- [x] 3.1 Create `ISymbolCacheBuilder` interface
- [x] 3.2 Implement symbol extraction from source files
- [x] 3.3 Implement symbol classification (types, methods, properties)
- [x] 3.4 Implement symbol location tracking (file, line, column)
- [x] 3.5 Implement cross-reference detection
- [x] 3.6 Write `cache/symbols.json` with deterministic ordering
- [x] 3.7 Unit tests: symbol cache is deterministic, covers all public symbols

## 4. FileGraphBuilder
- [x] 4.1 Create `IFileGraphBuilder` interface
- [x] 4.2 Implement project reference extraction
- [x] 4.3 Implement using/import dependency mapping
- [x] 4.4 Implement file-to-file relationship detection
- [x] 4.5 Implement edge weight calculation (coupling strength)
- [x] 4.6 Write `cache/file-graph.json` with deterministic ordering
- [x] 4.7 Unit tests: file graph correctly represents project dependencies

## 5. MapValidator
- [x] 5.1 Create `IMapValidator` interface
- [x] 5.2 Implement schema validation for all codebase artifacts
- [x] 5.3 Implement cross-file invariant checks
- [x] 5.4 Implement determinism verification (hash comparison)
- [x] 5.5 Implement completeness checks (required files present)
- [x] 5.6 Unit tests: validator catches invalid/incomplete maps

## 6. Handler Integration
- [x] 6.1 Create `CodebaseMapperHandler` for orchestrator integration
- [x] 6.2 Implement trigger conditions (new repo, stale map, explicit request)
- [x] 6.3 Register handler in `Gmsd.Agents` composition root
- [x] 6.4 Integration tests: handler works with orchestrator gating

## 7. CLI Tooling
- [x] 7.1 Add CLI command `openspec map-codebase` (full scan)
- [x] 7.2 Add CLI command `openspec validate-codebase` (validation only)
- [x] 7.3 Add CLI command `openspec refresh-symbols` (incremental symbol update)
- [x] 7.4 Add progress reporting for long-running scans
- [x] 7.5 Run `openspec validate --strict` and fix issues

## 8. Performance & Determinism
- [x] 8.1 Implement incremental scan (only changed files)
- [x] 8.2 Implement parallel processing where safe
- [x] 8.3 Add caching for intermediate results
- [x] 8.4 Verify byte-identical output for unchanged repository
- [x] 8.5 Benchmark scan performance on large solutions