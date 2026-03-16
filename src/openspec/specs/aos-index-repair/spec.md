# aos-index-repair Specification

## Purpose

Defines canonical AOS workspace contracts and behavioral semantics for $capabilityId.

- **Lives in:** `nirmata.Aos/*`, `.aos/**`
- **Owns:** Engine-level artifact contracts, validation, and deterministic IO semantics for this capability
- **Does not own:** Plane/orchestrator workflows (owned by `agents-*` capabilities)
## Requirements
### Requirement: Index repair CLI command exists
The system SHALL provide a CLI command `aos repair indexes` that deterministically rebuilds index artifacts in an AOS workspace rooted at `.aos/`.

#### Scenario: Index repair command is available
- **WHEN** `aos repair indexes --help` is executed
- **THEN** usage information is printed and the command exits successfully

### Requirement: Index repair rebuilds spec catalog indexes deterministically
`aos repair indexes` SHALL rebuild the spec catalog index files deterministically based on the current disk state under `.aos/spec/**`:
- `.aos/spec/milestones/index.json`
- `.aos/spec/phases/index.json`
- `.aos/spec/tasks/index.json`
- `.aos/spec/issues/index.json`
- `.aos/spec/uat/index.json`

The repaired index documents MUST conform to the catalog index contract:
- `schemaVersion` = 1
- `items` = array of artifact IDs (strings)

The `items` array MUST be sorted using **ordinal** string ordering.

#### Scenario: Repair rebuilds a missing or malformed spec catalog index
- **GIVEN** a workspace where one or more required spec catalog indexes are missing or contain invalid JSON
- **WHEN** `aos repair indexes` is executed
- **THEN** each affected index is rebuilt as valid deterministic JSON with deterministically sorted `items`

### Requirement: Index repair rebuilds the run index deterministically
`aos repair indexes` SHALL rebuild the run index at `.aos/evidence/runs/index.json` deterministically from run metadata on disk under `.aos/evidence/runs/<run-id>/run.json`.

The run index MUST be written using the canonical deterministic JSON writer defined by `aos-deterministic-json-serialization`.

#### Scenario: Repair rebuilds a missing run index
- **GIVEN** a workspace where `.aos/evidence/runs/<run-id>/run.json` exists for one or more runs and `.aos/evidence/runs/index.json` is missing
- **WHEN** `aos repair indexes` is executed
- **THEN** `.aos/evidence/runs/index.json` is rebuilt deterministically to enumerate those runs

### Requirement: Index repair writes deterministically and atomically
All index files written by `aos repair indexes` under `.aos/**` MUST be written using the canonical deterministic JSON writer defined by `aos-deterministic-json-serialization`, including:
- atomic write semantics (no partial/corrupt artifacts)
- no-churn semantics when canonical bytes are unchanged

#### Scenario: Repair does not rewrite unchanged canonical bytes
- **GIVEN** a workspace where all index files already match the canonical bytes that repair would produce
- **WHEN** `aos repair indexes` is executed
- **THEN** the command succeeds and no index file is rewritten

