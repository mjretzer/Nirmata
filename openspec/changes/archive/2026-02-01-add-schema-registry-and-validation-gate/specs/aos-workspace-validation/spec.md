## ADDED Requirements
### Requirement: Schema validation CLI is available
The system SHALL provide a CLI command `aos validate schemas` that validates the shipped schema pack and fails if any schema is malformed or violates canonical filename rules.

#### Scenario: Schema pack validation succeeds
- **GIVEN** the shipped schema pack is well-formed and canonically named
- **WHEN** `aos validate schemas` is executed
- **THEN** the command succeeds with exit code 0

#### Scenario: Schema pack validation fails on malformed schema JSON
- **GIVEN** a shipped schema asset contains malformed JSON
- **WHEN** `aos validate schemas` is executed
- **THEN** the command fails with a non-zero exit code and an actionable error indicating the failing schema

### Requirement: Workspace validation CLI is available
The system SHALL provide a CLI command `aos validate workspace` that validates an AOS workspace rooted at `.aos/`.

`aos validate workspace` MUST default to validating all layers:
- `spec`
- `state`
- `evidence`
- `codebase`
- `context`

#### Scenario: Workspace validation defaults to all layers
- **WHEN** `aos validate workspace` is executed without options
- **THEN** validation runs across all layers and reports any failures

### Requirement: Workspace validation supports explicit layer selection
`aos validate workspace` SHALL support an option `--layers` to validate an explicit subset of layers.

The `--layers` value MUST be a comma-separated list of layer names from:
`spec,state,evidence,codebase,context`.

#### Scenario: Workspace validation validates only selected layers
- **WHEN** `aos validate workspace --layers spec,state` is executed
- **THEN** only the `spec` and `state` layers are validated and other layers are not validated

### Requirement: Workspace validation fails fast on malformed JSON
Workspace validation MUST detect malformed JSON in required artifacts and fail before downstream workflows proceed.

#### Scenario: Malformed project.json is rejected
- **GIVEN** `.aos/spec/project.json` exists but is malformed JSON
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails with a non-zero exit code and reports the malformed JSON artifact

### Requirement: Workspace invariants are enforced (single-project model)
Workspace validation MUST enforce invariants for a single-project AOS workspace:
- `.aos/spec/project.json` MUST exist
- `.aos/spec/projects.json` MUST NOT exist
- `.aos/state/active-project.json` MUST NOT exist

#### Scenario: Missing project.json fails validation
- **GIVEN** `.aos/spec/project.json` is missing
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails with a non-zero exit code and reports the missing artifact

#### Scenario: Forbidden multi-project artifacts fail validation
- **GIVEN** `.aos/spec/projects.json` exists OR `.aos/state/active-project.json` exists
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails with a non-zero exit code and reports the forbidden artifact(s)

### Requirement: Roadmap does not reference multiple projects
If `.aos/spec/roadmap.json` exists, workspace validation MUST fail if the roadmap references any project other than the single project defined by `.aos/spec/project.json`.

#### Scenario: Multi-project roadmap reference fails validation
- **GIVEN** `.aos/spec/roadmap.json` references multiple projects (by ID, filename, or an array of projects)
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails with a non-zero exit code and reports that only a single project is permitted

