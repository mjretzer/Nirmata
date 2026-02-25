# aos-lock-manager Specification

## Purpose

Defines canonical AOS workspace contracts and behavioral semantics for $capabilityId.

- **Lives in:** `Gmsd.Aos/*`, `.aos/**`
- **Owns:** Engine-level artifact contracts, validation, and deterministic IO semantics for this capability
- **Does not own:** Plane/orchestrator workflows (owned by `agents-*` capabilities)
## Requirements
### Requirement: Exclusive workspace lock is held for the duration of the command lifecycle
The system SHALL ensure that an acquired exclusive workspace lock remains valid and unbroken until explicitly released by the owner.

The implementation MUST:
- Use an OS-level file locking mechanism (e.g., `FileShare.None`) that prevents other processes from opening, reading, or writing the lock file while it is held
- Maintain the file handle for the duration of the lock's existence
- Prevent lock hijacking or accidental release by other processes

#### Scenario: Lock handle prevents concurrent access
- **GIVEN** a process has successfully acquired the workspace lock
- **WHEN** a second process attempts to read or write the lock file at the canonical path
- **THEN** the second process receives an OS-level access violation or sharing violation error immediately

### Requirement: Lock release is atomic and identity-validated
The system SHALL ensure that a lock can only be released by the process that acquired it.

The implementation MUST:
- Validate that the `lockId` of the handle matches the `lockId` in the persisted lock file before deletion
- Perform the release as an atomic operation (closing the exclusive handle) to prevent race conditions during cleanup

#### Scenario: Identity-validated release succeeds
- **GIVEN** a process holds a lock handle with a specific `lockId`
- **WHEN** the process disposes the handle
- **THEN** the lock file is deleted from the canonical path
- **AND** the workspace becomes available for new lock acquisitions

### Requirement: Mutating commands require an exclusive workspace lock
The system SHALL enforce an exclusive workspace lock for any CLI command that mutates `.aos/**`.

Mutating commands include (at minimum):
- `aos init`
- `aos run start`
- `aos run finish`
- `aos run pause`
- `aos run resume`
- `aos execute-plan`
- `aos repair indexes`
- `aos checkpoint ...`
- `aos config ...` (commands that write under `.aos/config/**`)
- `aos cache clear`
- `aos cache prune`
- `aos secret set`
- `aos secret delete`

Validation commands (e.g., `aos validate ...`, `aos report-progress`, `aos secret list`, `aos secret get`) MUST NOT require the lock.

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

#### Scenario: Pause/resume commands require lock
- **GIVEN** the workspace lock is held by another process
- **WHEN** `aos run pause --run-id <id>` is executed
- **THEN** the command fails with lock-contention error

#### Scenario: Secret commands require lock for mutations
- **GIVEN** the workspace lock is held by another process
- **WHEN** `aos secret set mykey myvalue` is executed
- **THEN** the command fails with lock-contention error
- **AND** `aos secret list` succeeds (read-only, no lock required)

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

