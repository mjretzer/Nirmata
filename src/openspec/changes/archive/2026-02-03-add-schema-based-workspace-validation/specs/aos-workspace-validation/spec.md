## ADDED Requirements

### Requirement: Workspace validation is schema-based for known artifacts
`aos validate workspace` SHALL validate known AOS workspace artifacts structurally against the local schema pack (not just for well-formed JSON).

At minimum, schema-based validation MUST apply to baseline artifacts already written by `aos init`, including:
- `.aos/spec/project.json`
- `.aos/spec/roadmap.json`
- `.aos/spec/*/index.json` (catalog indexes)
- `.aos/state/state.json`
- `.aos/evidence/runs/index.json`
- `.aos/config/config.json` (when present)
- `.aos/config/policy.json` (when present / required by other commands)

#### Scenario: Workspace validation fails when an artifact violates its schema
- **GIVEN** `.aos/spec/project.json` exists and is valid JSON
- **AND** `.aos/spec/project.json` violates the project schema
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails and reports a schema validation issue for `.aos/spec/project.json`

### Requirement: Schema validation issues are reported in a normalized form
When schema validation fails, the system SHALL produce a normalized report for each issue including:
- contract path
- schema ID (`$id`)
- instance location (JSON Pointer / equivalent stable path)
- message (human readable)

#### Scenario: Schema validation reports include schema id and instance location
- **GIVEN** `.aos/spec/roadmap.json` contains an invalid value for a required property
- **WHEN** `aos validate workspace` is executed
- **THEN** the validation report includes the roadmap schema `$id` and the invalid property location

### Requirement: Roadmap items reference artifacts deterministically
If `.aos/spec/roadmap.json` exists, workspace validation MUST treat each `roadmap.items[]` as an artifact reference:
- `roadmap.items[].kind` MUST be a recognized artifact kind
- `roadmap.items[].id` MUST be a valid artifact ID for that kind

For each roadmap item, workspace validation MUST fail if the referenced artifact does not exist at the canonical contract path.

If a catalog index exists for that artifact kind, workspace validation MUST also fail if the referenced artifact ID is not present in the corresponding catalog index.

#### Scenario: Roadmap references a missing artifact
- **GIVEN** `.aos/spec/roadmap.json` contains an item `{ id: \"MS-0001\", kind: \"milestone\" }`
- **AND** `.aos/spec/milestones/MS-0001/milestone.json` does not exist
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails and reports the missing referenced artifact contract path

#### Scenario: Roadmap references an artifact missing from its catalog index
- **GIVEN** `.aos/spec/roadmap.json` contains an item `{ id: \"TSK-000001\", kind: \"task\" }`
- **AND** `.aos/spec/tasks/index.json` exists but does not include `TSK-000001`
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails and reports that the roadmap reference is not present in the tasks catalog index

### Requirement: Artifact kinds are enumerated in a stable public catalog
The system SHALL expose stable artifact kind strings in `nirmata.Aos.Public.Catalogs.ArtifactKinds` for use by validators and tooling.

The catalog MUST align with engine routing rules (artifact ID parsing and contract path routing).

#### Scenario: Validator recognizes roadmap kinds without ad-hoc string literals
- **WHEN** workspace validation validates a roadmap item kind
- **THEN** it uses `nirmata.Aos.Public.Catalogs.ArtifactKinds` to determine recognized kinds

## MODIFIED Requirements

### Requirement: Workspace validation fails fast on malformed JSON
Workspace validation MUST detect malformed JSON in required artifacts and fail before downstream workflows proceed.

Invalid JSON failures SHOULD be reported in the same normalized report model as schema validation (contract path + message), even when schema evaluation cannot proceed.

#### Scenario: Malformed project.json is rejected
- **GIVEN** `.aos/spec/project.json` exists but is malformed JSON
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails with a non-zero exit code and reports the malformed JSON artifact
