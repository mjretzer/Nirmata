## ADDED Requirements
### Requirement: `aos init` seeds baseline state and evidence artifacts
`aos init` SHALL seed the minimal required baseline artifacts for strict workspace validation:
- `.aos/state/state.json` (deterministic JSON)
- `.aos/state/events.ndjson` (empty file is permitted)
- `.aos/evidence/logs/commands.json` (deterministic JSON)
- `.aos/evidence/runs/index.json` (deterministic JSON)

#### Scenario: Baseline state and evidence artifacts exist after init
- **WHEN** `aos init` completes successfully
- **THEN** each baseline state/evidence artifact exists on disk in the expected path

## MODIFIED Requirements
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

