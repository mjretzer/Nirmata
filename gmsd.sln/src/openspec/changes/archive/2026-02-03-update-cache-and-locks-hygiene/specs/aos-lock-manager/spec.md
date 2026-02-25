## ADDED Requirements

### Requirement: Lock CLI primitives expose lock status, acquisition, and release
The system SHALL provide CLI primitives to inspect and manage the exclusive workspace lock:

- `aos lock status`
- `aos lock acquire`
- `aos lock release [--force]`

The commands MUST use the canonical lock contract path defined by `aos-path-routing`.

#### Scenario: Lock status reports unlocked when no lock file exists
- **GIVEN** the workspace is initialized
- **AND** the lock file does not exist at the canonical lock contract path
- **WHEN** `aos lock status` is executed
- **THEN** the command exits successfully
- **AND** the output indicates the workspace is unlocked

#### Scenario: Lock acquire creates the lock file and status reports locked
- **GIVEN** the workspace is initialized
- **AND** the lock file does not exist at the canonical lock contract path
- **WHEN** `aos lock acquire` is executed
- **THEN** the command exits successfully
- **AND** the lock file exists at the canonical lock contract path
- **AND** `aos lock status` indicates the workspace is locked

#### Scenario: Lock acquire fails deterministically when already locked
- **GIVEN** the workspace lock is already held by another process
- **WHEN** `aos lock acquire` is executed
- **THEN** the command fails fast with the stable lock-contention exit code
- **AND** the error output is actionable (identifies the canonical lock path and next steps)

#### Scenario: Lock release removes the lock file
- **GIVEN** the lock file exists at the canonical lock contract path
- **WHEN** `aos lock release` is executed
- **THEN** the command exits successfully
- **AND** the lock file no longer exists

#### Scenario: Lock release without force fails on an unparseable lock file
- **GIVEN** the lock file exists at the canonical lock contract path
- **AND** the lock file content is not parseable as the expected workspace-lock document
- **WHEN** `aos lock release` is executed without `--force`
- **THEN** the command fails with an actionable error describing how to force release

## MODIFIED Requirements

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
- `aos cache clear`
- `aos cache prune`

Validation commands (e.g., `aos validate ...`) MUST NOT require the lock.

The lock artifact MUST be persisted at the canonical lock contract path defined by `aos-path-routing` (under `.aos/locks/**`) and MUST be actionable (it must highlight who/what holds the lock and how to release it).

Lock contention behavior MUST be deterministic and fail fast (no unbounded waiting).

#### Scenario: Mutating command fails fast when the lock is held
- **GIVEN** the workspace lock is held by another process
- **WHEN** `aos run start` is executed
- **THEN** the command fails with a stable lock-contention exit code and an actionable error describing the lock holder and next steps

#### Scenario: Validation is not blocked by a held lock
- **GIVEN** the workspace lock is held by another process
- **WHEN** `aos validate workspace` is executed
- **THEN** the command runs normally (success or failure depends only on validation results)

