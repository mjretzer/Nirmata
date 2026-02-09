## MODIFIED Requirements
### Requirement: AOS workspace can be bootstrapped via `aos init`
The system SHALL provide a CLI command `aos init` that bootstraps an AOS workspace rooted at `.aos/` in the current repository.

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
- `.aos/schemas/`

#### Scenario: First-time initialization creates the canonical workspace structure
- **WHEN** `aos init` is executed in a repository with no existing `.aos/` folder
- **THEN** the `.aos/` folder exists with all canonical subfolders present

#### Scenario: `aos init` fails when executed outside a repository
- **WHEN** `aos init` is executed in a directory that is not within a repository
- **THEN** the command fails with an actionable error explaining that a repository root could not be determined

#### Scenario: Init writes deterministic JSON atomically
- **WHEN** `aos init` completes successfully
- **THEN** every JSON artifact written by init under `.aos/**` is valid JSON and was written using the canonical deterministic JSON writer
