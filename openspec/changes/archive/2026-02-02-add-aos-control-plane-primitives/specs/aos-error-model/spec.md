## ADDED Requirements
### Requirement: CLI exit codes are stable and documented
The system SHALL use stable exit codes for AOS CLI commands:
- `0` success
- `1` invalid usage/options
- `2` validation or execution failure (known failure)
- `3` policy violation
- `4` lock contention (unable to acquire required lock)
- `5` unexpected internal error

#### Scenario: Invalid CLI usage returns exit code 1
- **WHEN** `aos execute-plan` is executed without required options
- **THEN** usage is printed and the process exits with code 1

### Requirement: Errors are normalized into a machine-readable envelope
When a command fails (non-zero exit code), the system SHALL produce an actionable error message and SHALL map failures to a normalized error envelope containing at least:
- `code` (stable identifier)
- `message` (human-readable, actionable)
- `details` (optional structured context; e.g., contract path, option name)

#### Scenario: Workspace validation failure maps to a normalized error code
- **GIVEN** the workspace is missing required artifacts under `.aos/**`
- **WHEN** a mutating command that requires a valid workspace is executed
- **THEN** the command fails with exit code 2 and emits an actionable error indicating the failing contract path(s)

