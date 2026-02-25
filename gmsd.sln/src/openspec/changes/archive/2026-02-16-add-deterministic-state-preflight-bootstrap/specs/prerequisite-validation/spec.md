## ADDED Requirements
### Requirement: Preflight validator enforces runtime state readiness
The prerequisite/preflight validator SHALL verify runtime state readiness for orchestrator execution by ensuring baseline state artifacts exist or can be repaired deterministically.

Preflight readiness checks MUST cover:
- `.aos/state/state.json` presence and valid JSON shape
- `.aos/state/events.ndjson` existence
- optional deterministic snapshot derivation from events when snapshot is missing or stale

#### Scenario: Missing state snapshot is repaired during preflight
- **GIVEN** `.aos/state/events.ndjson` exists and is valid
- **AND** `.aos/state/state.json` is missing
- **WHEN** preflight validation runs
- **THEN** it repairs readiness by creating or deriving `state.json`
- **AND** returns a satisfied preflight result

#### Scenario: Missing event log is repaired during preflight
- **GIVEN** `.aos/state/state.json` exists
- **AND** `.aos/state/events.ndjson` is missing
- **WHEN** preflight validation runs
- **THEN** it creates an empty append-ready events log
- **AND** returns a satisfied preflight result

### Requirement: Preflight failures include conversationally actionable recovery metadata
When runtime state readiness cannot be established deterministically, prerequisite validation SHALL return structured failure metadata suitable for conversational recovery.

Failure metadata MUST include:
- `failureCode` identifying the preflight failure category
- `failingPrerequisite` identifying the specific prerequisite that could not be satisfied
- `attemptedRepairs` listing deterministic repair steps that were attempted
- `suggestedFixes` with one or more actionable commands or remediation actions
- a human-readable explanation suitable for assistant output

#### Scenario: Irreparable state readiness failure returns structured recovery details
- **GIVEN** `.aos/state/events.ndjson` is present but contains invalid NDJSON that blocks deterministic derivation
- **WHEN** preflight validation runs
- **THEN** it returns an unsatisfied result with `failureCode` for state-readiness failure
- **AND** includes `failingPrerequisite`, `attemptedRepairs`, and `suggestedFixes`
- **AND** does not mark prerequisites satisfied

#### Scenario: User receives actionable next steps instead of unstructured runtime error
- **GIVEN** preflight readiness fails after deterministic repair attempts
- **WHEN** the orchestrator renders prerequisite failure output
- **THEN** assistant output includes the structured diagnostic fields and actionable suggested fixes
- **AND** the user-facing response does not surface a generic low-context runtime error
