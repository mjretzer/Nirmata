## MODIFIED Requirements
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
