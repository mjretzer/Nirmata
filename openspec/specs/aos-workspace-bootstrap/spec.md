# aos-workspace-bootstrap Specification

## Purpose
TBD - created by archiving change add-aos-workspace-bootstrap. Update Purpose after archive.
## Requirements
### Requirement: AOS workspace can be bootstrapped via `aos init`
The system SHALL provide a CLI command `aos init` that bootstraps an AOS workspace rooted at `.aos/` in the current repository.

When `--root` is not provided, `aos init` MUST deterministically discover the repository root by walking parent directories from the current working directory until a repository marker is found.
Accepted repository markers MUST include:
- a `.git/` directory
- a `Gmsd.slnx` file

If no marker is found, `aos init` MUST fail with an actionable error and MUST NOT silently treat the starting directory as the repository root.

All files created by `aos init` MUST be deterministic across machines:
- Text files MUST be UTF-8 (without BOM) and MUST use LF (`\n`) line endings.
- JSON files MUST be valid JSON and MUST be written using the canonical deterministic JSON writer defined by `aos-deterministic-json-serialization`:
  - stable recursive key ordering (canonical JSON)
  - stable formatting (so byte-for-byte fixture comparison is possible)
  - atomic write semantics (no partial/corrupt artifacts)
  - no-churn semantics when canonical bytes are unchanged

`aos init` SHALL create the canonical workspace subfolders:
- `.aos/spec/`
- `.aos/state/`
- `.aos/evidence/`
- `.aos/context/`
- `.aos/codebase/`
- `.aos/cache/`
- `.aos/config/`
- `.aos/schemas/`
- `.aos/locks/`

#### Scenario: First-time initialization creates the canonical workspace structure
- **WHEN** `aos init` is executed in a repository with no existing `.aos/` folder
- **THEN** the `.aos/` folder exists with all canonical subfolders present

#### Scenario: `aos init` fails when executed outside a repository
- **WHEN** `aos init` is executed in a directory that is not within a repository
- **THEN** the command fails with an actionable error explaining that a repository root could not be determined

#### Scenario: Init writes deterministic JSON atomically
- **WHEN** `aos init` completes successfully
- **THEN** every JSON artifact written by init under `.aos/**` is valid JSON and was written using the canonical deterministic JSON writer

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
- `.aos/spec/issues/index.json`
- `.aos/spec/uat/index.json`

The baseline index files MUST be valid JSON and deterministic.

#### Scenario: Baseline indexes exist
- **WHEN** `aos init` completes successfully
- **THEN** each baseline index file exists and contains valid, deterministic JSON

### Requirement: `aos init` stubs local schema registry wiring
`aos init` SHALL seed a local schema pack under `.aos/schemas/**` so later validation commands can load and validate schemas without requiring external services.

`aos init` MUST create:
- `.aos/schemas/registry.json` as deterministic, valid JSON
- one or more deterministic schema files under `.aos/schemas/*.schema.json` (canonically named)

#### Scenario: Local schema pack templates exist after init
- **WHEN** `aos init` completes successfully
- **THEN** `.aos/schemas/registry.json` exists, is valid JSON, and lists one or more `.schema.json` files that exist under `.aos/schemas/`

#### Scenario: Local schema pack registry is non-empty and references existing files
- **GIVEN** a repository where `aos init` has completed successfully
- **WHEN** the user inspects `.aos/schemas/registry.json`
- **THEN** `schemas` is a non-empty array and every listed filename exists as a file under `.aos/schemas/`

### Requirement: `aos init` is idempotent and fails fast on non-compliant workspaces
`aos init` MUST be safe to run multiple times.

- If `.aos/` exists and is compliant with the canonical workspace contract, `aos init` MUST succeed without modifying existing artifacts.
- If `.aos/` exists and is not compliant, `aos init` MUST fail with an error describing the missing/extra/invalid paths.

For this milestone, a workspace is compliant if and only if:
- the canonical subfolders exist and are directories
- `.aos/spec/project.json` exists and is valid JSON
- `.aos/spec/roadmap.json` exists and is valid JSON
- each baseline spec catalog index exists, is a file, and is valid JSON:
  - `.aos/spec/milestones/index.json`
  - `.aos/spec/phases/index.json`
  - `.aos/spec/tasks/index.json`
  - `.aos/spec/issues/index.json`
  - `.aos/spec/uat/index.json`
- the baseline state artifacts exist:
  - `.aos/state/state.json` exists and is valid JSON
  - `.aos/state/events.ndjson` exists (empty is permitted)
- the baseline evidence artifacts exist:
  - `.aos/evidence/logs/commands.json` exists and is valid JSON
  - `.aos/evidence/runs/index.json` exists and is valid JSON
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

### Requirement: `aos init` seeds deterministic default spec templates
`aos init` SHALL seed deterministic default spec templates under `.aos/spec/**`.

For this milestone, `aos init` MUST create `.aos/spec/roadmap.json`.

#### Scenario: Default roadmap template exists after init
- **WHEN** `aos init` completes successfully
- **THEN** `.aos/spec/roadmap.json` exists and contains valid, deterministic JSON

### Requirement: `aos init` seeds baseline state and evidence artifacts
`aos init` SHALL seed the minimal required baseline artifacts for strict workspace validation:
- `.aos/state/state.json` (deterministic JSON)
- `.aos/state/events.ndjson` (empty file is permitted)
- `.aos/evidence/logs/commands.json` (deterministic JSON)
- `.aos/evidence/runs/index.json` (deterministic JSON)

#### Scenario: Baseline state and evidence artifacts exist after init
- **WHEN** `aos init` completes successfully
- **THEN** each baseline state/evidence artifact exists on disk in the expected path

### Requirement: `aos init` seeds a baseline policy contract
`aos init` SHALL seed a deterministic baseline policy file at `.aos/config/policy.json`.

The policy file MUST be valid JSON and MUST be written using the canonical deterministic JSON writer.

#### Scenario: Baseline policy exists after init
- **WHEN** `aos init` completes successfully
- **THEN** `.aos/config/policy.json` exists and contains valid, deterministic JSON

