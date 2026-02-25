# aos-workspace-validation Specification

## Purpose

Defines canonical AOS workspace contracts and behavioral semantics for $capabilityId.

- **Lives in:** `Gmsd.Aos/*`, `.aos/**`
- **Owns:** Engine-level artifact contracts, validation, and deterministic IO semantics for this capability
- **Does not own:** Plane/orchestrator workflows (owned by `agents-*` capabilities)
## Requirements
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
- `config`

#### Scenario: Workspace validation defaults to all layers
- **WHEN** `aos validate workspace` is executed without options
- **THEN** validation runs across all layers and reports any failures

### Requirement: Workspace validation supports explicit layer selection
`aos validate workspace` SHALL support an option `--layers` to validate an explicit subset of layers.

The `--layers` value MUST be a comma-separated list of layer names from:
`spec,state,evidence,codebase,context,config`.

#### Scenario: Workspace validation validates only selected layers
- **WHEN** `aos validate workspace --layers spec,state` is executed
- **THEN** only the `spec` and `state` layers are validated and other layers are not validated

### Requirement: Workspace validation fails fast on malformed JSON
Workspace validation MUST detect malformed JSON in required artifacts and fail before downstream workflows proceed.

Invalid JSON failures SHOULD be reported in the same normalized report model as schema validation (contract path + message), even when schema evaluation cannot proceed.

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

### Requirement: Workspace validation requires baseline spec catalog indexes
Workspace validation MUST enforce that required spec catalog indexes exist and are valid JSON:
- `.aos/spec/milestones/index.json`
- `.aos/spec/phases/index.json`
- `.aos/spec/tasks/index.json`
- `.aos/spec/issues/index.json`
- `.aos/spec/uat/index.json`

#### Scenario: Missing required spec catalog index fails validation
- **GIVEN** `.aos/spec/issues/index.json` is missing
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails with a non-zero exit code and reports the missing artifact

### Requirement: Workspace validation requires baseline state artifacts
Workspace validation MUST enforce that the baseline state artifacts exist:
- `.aos/state/state.json` MUST exist and MUST be valid JSON
- `.aos/state/events.ndjson` MUST exist

If `.aos/state/events.ndjson` contains any non-empty lines:
- the file MUST be newline-delimited JSON (NDJSON)
- each non-empty line MUST be valid JSON
- each non-empty line MUST be a single JSON object (one object per line)
- each non-empty line MUST validate against the local event schema (`gmsd:aos:schema:event:v1`)

#### Scenario: Malformed state.json fails validation
- **GIVEN** `.aos/state/state.json` exists but is malformed JSON
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails with a non-zero exit code and reports the malformed JSON artifact

#### Scenario: Malformed NDJSON event line fails validation
- **GIVEN** `.aos/state/events.ndjson` contains a non-empty line that is not valid JSON
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails with a non-zero exit code and reports the invalid event log line

#### Scenario: Non-object NDJSON event line fails validation
- **GIVEN** `.aos/state/events.ndjson` contains a non-empty line that parses as JSON but is not an object
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails with a non-zero exit code and reports that the event line must be a JSON object

#### Scenario: Event schema violation fails validation
- **GIVEN** `.aos/state/events.ndjson` contains a non-empty line with `schemaVersion` that violates `gmsd:aos:schema:event:v1`
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails with a non-zero exit code and reports a schema validation issue for `.aos/state/events.ndjson`

### Requirement: Workspace validation requires baseline evidence artifacts
Workspace validation MUST enforce that baseline evidence artifacts exist and are valid JSON:
- `.aos/evidence/logs/commands.json`
- `.aos/evidence/runs/index.json`

Workspace validation SHOULD validate additional evidence artifacts when present, including:
- `.aos/evidence/runs/<run-id>/summary.json`
- `.aos/evidence/runs/<run-id>/commands.json`
- `.aos/evidence/runs/<run-id>/artifacts/manifest.json`
- `.aos/evidence/task-evidence/<task-id>/latest.json`

During the transition period, workspace validation SHOULD tolerate legacy run layouts created before the restructured PH-ENG-0006 layout, while still enforcing the baseline evidence artifacts above.

#### Scenario: Missing evidence command log fails validation
- **GIVEN** `.aos/evidence/logs/commands.json` is missing
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails with a non-zero exit code and reports the missing artifact

### Requirement: Workspace validation validates config when present
Workspace validation MUST treat config as an optional layer: config validation MUST NOT fail solely because `.aos/config/config.json` is missing.

If `.aos/config/config.json` exists, workspace validation MUST validate it against the local schema pack.

#### Scenario: Missing config does not fail validation
- **GIVEN** a compliant workspace where `.aos/config/config.json` does not exist
- **WHEN** `aos validate workspace` is executed
- **THEN** validation does not report a failure solely due to missing config

#### Scenario: Present config is schema-validated
- **GIVEN** `.aos/config/config.json` exists
- **WHEN** `aos validate workspace` is executed
- **THEN** validation reports a failure if the config is invalid JSON or does not conform to the config schema

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
The system SHALL expose stable artifact kind strings in `Gmsd.Aos.Public.Catalogs.ArtifactKinds` for use by validators and tooling.

The catalog MUST align with engine routing rules (artifact ID parsing and contract path routing).

#### Scenario: Validator recognizes roadmap kinds without ad-hoc string literals
- **WHEN** workspace validation validates a roadmap item kind
- **THEN** it uses `Gmsd.Aos.Public.Catalogs.ArtifactKinds` to determine recognized kinds

### Requirement: Workspace validation enforces cursor reference invariants
When `.aos/state/state.json` includes a cursor reference (`cursor.kind` + `cursor.id`), `aos validate workspace` MUST validate that the cursor reference is deterministic and resolvable.

Workspace validation MUST fail if:
- `cursor.kind` is present without `cursor.id` (or vice versa)
- `cursor.kind` is not a recognized artifact kind
- `cursor.id` cannot be parsed as an id for the given kind, or is not canonical for that kind
- the referenced artifact does not exist at the canonical contract path for the kind/id

If a catalog index exists for the referenced kind, workspace validation MUST also fail if the cursor id is not present in that catalog index.

#### Scenario: Cursor kind is required when cursor id is present
- **GIVEN** `.aos/state/state.json` contains `cursor.id` but omits `cursor.kind`
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails and reports a malformed cursor reference

#### Scenario: Cursor rejects an unrecognized kind deterministically
- **GIVEN** `.aos/state/state.json` contains `cursor.kind = "unknown-kind"` and `cursor.id = "X-0001"`
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails and reports that the cursor kind is not recognized (including the list of expected kinds)

#### Scenario: Cursor reference to a missing artifact fails deterministically
- **GIVEN** `.aos/state/state.json` contains a cursor reference `{ kind: "milestone", id: "MS-0001" }`
- **AND** the referenced milestone does not exist at its canonical contract path
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails and reports the missing referenced artifact contract path

### Requirement: Workspace validation validates context pack artifacts when present
When `.aos/context/packs/**` contains context pack artifacts, `aos validate workspace` MUST validate each canonical pack file against the local schema pack using schema id `gmsd:aos:schema:context-pack:v1`.

Context pack files are canonical when named `PCK-####.json` and located under `.aos/context/packs/`.

Workspace validation MUST report schema failures using the normalized schema issue form (contract path, schema id, instance location, message).

#### Scenario: Present context pack is schema-validated
- **GIVEN** `.aos/context/packs/PCK-0001.json` exists
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails if the pack is invalid JSON or violates the context pack schema

#### Scenario: Context pack schema violations report instance location
- **GIVEN** `.aos/context/packs/PCK-0001.json` exists and violates a required property in the context pack schema
- **WHEN** `aos validate workspace` is executed
- **THEN** the validation report includes the context pack schema id and the invalid property location

