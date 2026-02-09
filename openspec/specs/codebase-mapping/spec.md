# codebase-mapping Specification

## Purpose
TBD - created by archiving change add-codebase-mapper-workflow. Update Purpose after archive.
## Requirements
### Requirement: Codebase intelligence pack is written to `.aos/codebase/**`
The system SHALL produce a codebase intelligence pack under `.aos/codebase/` containing structured JSON artifacts that describe the repository.

The intelligence pack MUST include:
- `map.json` — high-level codebase overview and metadata
- `stack.json` — detected technology stack
- `architecture.json` — architectural patterns and boundaries
- `structure.json` — directory/file structure with statistics
- `conventions.json` — coding conventions and patterns
- `testing.json` — test structure and coverage information
- `integrations.json` — external integration points
- `concerns.json` — cross-cutting concerns and complexity hotspots
- `cache/symbols.json` — symbol index for fast lookup
- `cache/file-graph.json` — file dependency graph

#### Scenario: Full codebase scan produces complete intelligence pack
- **GIVEN** a valid .NET solution repository
- **WHEN** the Codebase Mapper workflow executes
- **THEN** all required JSON files exist under `.aos/codebase/`
- **AND** each file is valid JSON matching its schema
- **AND** all files are written using deterministic JSON serialization

### Requirement: Symbol cache provides fast, deterministic symbol lookup
The system SHALL maintain a symbol cache at `.aos/codebase/cache/symbols.json` that enables fast lookup of type and member definitions.

The symbol cache MUST:
- include all public and internal types
- record symbol kind (class, interface, struct, enum, method, property, etc.)
- record location (file path, line, column)
- record containing namespace and assembly
- support cross-reference detection (which symbols reference which)
- be written with deterministic ordering (alphabetical by fully qualified name)

#### Scenario: Symbol lookup finds type definition
- **GIVEN** a codebase with a type `Gmsd.Agents.ICodebaseScanner`
- **WHEN** querying the symbol cache for this type
- **THEN** the cache returns the file path, line number, and symbol metadata
- **AND** the same query on a rebuilt cache yields identical results

### Requirement: File graph captures project-level dependencies
The system SHALL maintain a file dependency graph at `.aos/codebase/cache/file-graph.json` that captures relationships between files and projects.

The file graph MUST:
- include nodes for projects, directories, and source files
- include edges for project references, using statements, and file includes
- calculate edge weights representing coupling strength
- support path traversal (which files depend on a given file)
- be written with deterministic ordering (by source then target node)

#### Scenario: File graph correctly shows project reference chain
- **GIVEN** a solution where Project A references Project B
- **WHEN** the file graph is built
- **THEN** an edge exists from Project A to Project B
- **AND** the edge weight reflects the strength of the dependency

### Requirement: Codebase scan is deterministic for unchanged repository
The system SHALL guarantee that scanning the same repository state produces byte-identical output across multiple runs.

Determinism requirements:
- all JSON output must use canonical deterministic JSON writer
- all collections must be sorted alphabetically before serialization
- file discovery must use stable ordering
- timestamps in output must use UTC with fixed precision

#### Scenario: Re-scan produces identical output
- **GIVEN** a codebase at a specific git commit
- **WHEN** the Codebase Mapper runs twice on this state
- **THEN** the MD5 hashes of all output files match between runs

### Requirement: Validation ensures codebase pack integrity
The system SHALL validate the codebase intelligence pack against schemas and cross-file invariants.

Validation MUST check:
- all required files are present
- all files validate against their schemas
- symbol cache references point to existing files
- file graph edges reference existing nodes
- no orphaned or duplicate entries exist

#### Scenario: Validation catches missing required file
- **GIVEN** a codebase pack missing `stack.json`
- **WHEN** validation runs
- **THEN** validation fails with an error indicating the missing file

### Requirement: Incremental updates support fast refresh
The system SHALL support incremental updates to the codebase pack when only a subset of files has changed.

Incremental mode MUST:
- detect changed files using git status or file timestamps
- update affected symbols without full re-scan
- update affected file graph edges without full rebuild
- preserve unchanged portions of the pack
- maintain determinism for the updated output

#### Scenario: Incremental update after single file change
- **GIVEN** a complete codebase pack and a single modified source file
- **WHEN** incremental update runs
- **THEN** only symbols from the modified file are re-extracted
- **AND** file graph updates only affected edges
- **AND** output remains deterministic

