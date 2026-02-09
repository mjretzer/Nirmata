## ADDED Requirements
### Requirement: Mutating commands require an exclusive workspace lock
The system SHALL enforce an exclusive workspace lock for any CLI command that mutates `.aos/**`.

Mutating commands include (at minimum):
- `aos init`
- `aos run start`
- `aos run finish`
- `aos execute-plan`
- `aos repair indexes`
- `aos checkpoint ...`
- `aos config ...` (commands that write under `.aos/config/**`)

Validation commands (e.g., `aos validate ...`) MUST NOT require the lock.

The lock artifact MUST be persisted under `.aos/locks/**` and MUST be actionable (it must highlight who/what holds the lock and how to release it).

#### Scenario: Mutating command fails fast when the lock is held
- **GIVEN** the workspace lock is held by another process
- **WHEN** `aos run start` is executed
- **THEN** the command fails with a stable lock-contention exit code and an actionable error describing the lock holder and next steps

#### Scenario: Validation is not blocked by a held lock
- **GIVEN** the workspace lock is held by another process
- **WHEN** `aos validate workspace` is executed
- **THEN** the command runs normally (success or failure depends only on validation results)

