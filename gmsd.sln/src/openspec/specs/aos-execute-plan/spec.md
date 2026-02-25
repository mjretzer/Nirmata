# aos-execute-plan Specification

## Purpose

Defines canonical AOS workspace contracts and behavioral semantics for $capabilityId.

- **Lives in:** `Gmsd.Aos/*`, `.aos/**`
- **Owns:** Engine-level artifact contracts, validation, and deterministic IO semantics for this capability
- **Does not own:** Plane/orchestrator workflows (owned by `agents-*` capabilities)
## Requirements
### Requirement: Execute-plan CLI command exists
The system SHALL provide a CLI command `aos execute-plan` that executes a persisted plan file and produces deterministic outputs and evidence.

#### Scenario: Execute-plan command is available
- **WHEN** `aos execute-plan --help` is executed
- **THEN** usage information is printed and the command exits successfully

### Requirement: Execute-plan reads a persisted plan file
`aos execute-plan` SHALL accept an option `--plan <path>` that identifies a persisted plan file to execute.

The plan file MUST be valid JSON and MUST have the shape:
- `schemaVersion` (integer; currently `1`)
- `outputs` (array)

Each `outputs[]` entry MUST include:
- `relativePath` (string; relative file path to be written under the run outputs folder)
- `contentsUtf8` (string; file contents)

#### Scenario: Execute-plan loads a valid plan file
- **GIVEN** a valid plan file on disk
- **WHEN** `aos execute-plan --plan <path>` is executed
- **THEN** the command loads the plan and proceeds to execute it

#### Scenario: Execute-plan rejects malformed plan JSON
- **GIVEN** a plan file containing malformed JSON
- **WHEN** `aos execute-plan --plan <path>` is executed
- **THEN** the command fails with a non-zero exit code and an actionable error

### Requirement: Execute-plan auto-starts and auto-finishes a run
`aos execute-plan` SHALL start a new run automatically using the run lifecycle evidence scaffold.

On successful completion, `aos execute-plan` SHALL finish the run so run evidence metadata and the run index reflect a finished run.

#### Scenario: Execute-plan creates and finishes a run
- **GIVEN** an initialized `.aos/` workspace
- **WHEN** `aos execute-plan --plan <path>` executes successfully
- **THEN** a new `.aos/evidence/runs/<run-id>/` folder exists and the run is finished

### Requirement: Execute-plan writes outputs only within the run outputs folder
`aos execute-plan` MUST write output files only beneath:
`.aos/evidence/runs/<run-id>/outputs/**`.

The executor MUST reject any plan output entry whose `relativePath` attempts to:
- escape the outputs folder (e.g. contains `..`)
- target an absolute path

#### Scenario: Execute-plan enforces outputs-only scope
- **GIVEN** a plan whose outputs contain only safe relative paths
- **WHEN** `aos execute-plan --plan <path>` executes
- **THEN** all output files are created under `.aos/evidence/runs/<run-id>/outputs/**`

#### Scenario: Execute-plan rejects traversal paths
- **GIVEN** a plan file with an output `relativePath` that attempts traversal (e.g. `../x.txt`)
- **WHEN** `aos execute-plan --plan <path>` is executed
- **THEN** the command fails with a non-zero exit code and an actionable error describing the rejected path

### Requirement: Execute-plan records evidence of actions taken
`aos execute-plan` SHALL write an actions log under:
`.aos/evidence/runs/<run-id>/logs/**`
that records what outputs were written.

The actions log MUST be deterministic for identical inputs (except run IDs and timestamps).

The actions log MUST include a machine-readable file at:
`.aos/evidence/runs/<run-id>/logs/execute-plan.actions.json`
with the shape:
- `schemaVersion` (integer; currently `1`)
- `outputsWritten` (array of strings; output relative paths)

The actions log JSON MUST be written using the canonical deterministic JSON writer defined by `aos-deterministic-json-serialization`, including:
- UTF-8 (without BOM)
- LF (`\\n`) line endings
- valid JSON
- stable recursive key ordering and stable formatting (so fixture comparison is possible)
- atomic write semantics (no partial/corrupt artifacts)
- no-churn semantics when canonical bytes are unchanged

`outputsWritten` MUST be sorted using ordinal string ordering.

#### Scenario: Execute-plan writes an actions log
- **WHEN** `aos execute-plan --plan <path>` executes
- **THEN** an actions log exists under `.aos/evidence/runs/<run-id>/logs/**`

