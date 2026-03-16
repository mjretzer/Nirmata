## ADDED Requirements

### Requirement: AOS workspace can be bootstrapped via `aos init`
The system SHALL provide a CLI command `aos init` that bootstraps an AOS workspace rooted at `.aos/` in the current repository.

All files created by `aos init` MUST be deterministic across machines:
- Text files MUST be UTF-8 (without BOM) and MUST use LF (`\n`) line endings.
- JSON files MUST be valid JSON and MUST be written with stable key ordering and stable formatting (so byte-for-byte fixture comparison is possible).

`aos init` SHALL create the canonical workspace subfolders:
- `.aos/spec/`
- `.aos/state/`
- `.aos/evidence/`
- `.aos/context/`
- `.aos/codebase/`
- `.aos/cache/`
- `.aos/schemas/`

#### Scenario: First-time initialization creates the canonical workspace structure
- **WHEN** `aos init` is executed in a repository with no existing `.aos/` folder
- **THEN** the `.aos/` folder exists with all canonical subfolders present

#### Scenario: `aos init` fails when executed outside a repository
- **WHEN** `aos init` is executed in a directory that is not within a repository
- **THEN** the command fails with an actionable error explaining that a repository root could not be determined

### Requirement: `aos init` seeds the minimal project spec
`aos init` SHALL write a minimal project specification to `.aos/spec/project.json`.

The generated `project.json` MUST:
- be valid JSON
- be deterministic (no timestamps, usernames, machine IDs, absolute paths)

#### Scenario: Project skeleton is created deterministically
- **WHEN** `aos init` completes successfully
- **THEN** `.aos/spec/project.json` exists and contains valid, deterministic JSON

### Requirement: `aos init` creates baseline index files for spec catalogs
`aos init` SHALL create baseline index files for spec catalogs:
- `.aos/spec/milestones/index.json`
- `.aos/spec/phases/index.json`
- `.aos/spec/tasks/index.json`

The baseline index files MUST be valid JSON and deterministic.

#### Scenario: Baseline indexes exist
- **WHEN** `aos init` completes successfully
- **THEN** each baseline index file exists and contains valid, deterministic JSON

### Requirement: `aos init` stubs local schema registry wiring
`aos init` SHALL create placeholder schema registry artifacts under `.aos/schemas/` so that later commands can load a local registry without requiring external services.

`aos init` MUST create `.aos/schemas/registry.json` as a deterministic, valid JSON placeholder.

#### Scenario: Schema registry placeholders exist
- **WHEN** `aos init` completes successfully
- **THEN** `.aos/schemas/registry.json` exists and contains valid, deterministic JSON

### Requirement: `aos init` is idempotent and fails fast on non-compliant workspaces
`aos init` MUST be safe to run multiple times.

- If `.aos/` exists and is compliant with the canonical workspace contract, `aos init` MUST succeed without modifying existing artifacts.
- If `.aos/` exists and is not compliant, `aos init` MUST fail with an error describing the missing/extra/invalid paths.

For this milestone, a workspace is compliant if and only if:
- the canonical subfolders exist and are directories
- `.aos/spec/project.json` exists and is valid JSON
- each baseline spec catalog index exists, is a file, and is valid JSON:
  - `.aos/spec/milestones/index.json`
  - `.aos/spec/phases/index.json`
  - `.aos/spec/tasks/index.json`
- `.aos/schemas/registry.json` exists and is valid JSON
- `.aos/` contains no additional top-level entries beyond the canonical subfolders listed in this specification

#### Scenario: Re-running init on a compliant workspace is a no-op
- **GIVEN** a repository where `.aos/` is compliant
- **WHEN** `aos init` is executed
- **THEN** the command succeeds without changing existing `.aos/*` content

#### Scenario: Re-running init on a non-compliant workspace fails with actionable diagnostics
- **GIVEN** a repository where `.aos/` exists but is non-compliant
- **WHEN** `aos init` is executed
- **THEN** the command fails with an error describing the non-compliance

