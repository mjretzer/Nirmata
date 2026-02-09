## ADDED Requirements
### Requirement: Policy enforcement gates exist for execution
The system SHALL enforce explicit policies before performing any execution that could mutate the workspace or invoke tools/providers.

Policy enforcement MUST include (at minimum):
- a scope allowlist (what filesystem locations may be written)
- a tool allowlist (what tools/providers may be invoked)
- a no-implicit-state rule (execution MUST rely only on persisted inputs/artifacts, not chat state)

Policy violations MUST fail fast with a stable policy-violation exit code and an actionable error.

#### Scenario: Tool invocation is rejected when not allowlisted
- **GIVEN** an effective policy where a tool is not allowlisted
- **WHEN** execution attempts to invoke that tool
- **THEN** the operation fails with exit code 3 and an actionable error identifying the forbidden tool

