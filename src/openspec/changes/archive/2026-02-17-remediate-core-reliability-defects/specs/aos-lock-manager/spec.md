# Spec Delta: Lock Synchronization

## MODIFIED Requirements

### Requirement: Exclusive workspace lock is held for the duration of the command lifecycle
The system SHALL ensure that an acquired exclusive workspace lock remains valid and unmodifiable by other processes until explicitly released by the owner.

The implementation MUST:
- Use an OS-level file locking mechanism (e.g., `FileShare.None`) that prevents other processes from opening, reading, or writing the lock file while it is held.
- Maintain the file handle for the duration of the lock's existence.
- Release the lock by closing the file handle and deleting the file.

#### Scenario: Lock handle prevents concurrent access
- **GIVEN** a process has successfully acquired the workspace lock
- **WHEN** a second process attempts to read or write the lock file at the canonical path
- **THEN** the second process receives an OS-level access violation or sharing violation error immediately
- **AND** the second process fails with the stable lock-contention exit code

### Requirement: Lock release is atomic and identity-validated
The system SHALL ensure that a lock can only be released by the process that acquired it, or via an explicit force operation.

The implementation MUST:
- Validate that the `lockId` of the handle matches the `lockId` in the persisted lock file before deletion.
- Perform the release as an atomic operation (closing the exclusive handle) to prevent race conditions during cleanup.

#### Scenario: Identity-validated release succeeds
- **GIVEN** a process holds a lock handle with a specific `lockId`
- **WHEN** the process disposes the handle
- **THEN** the lock file is deleted from the canonical path
- **AND** the workspace becomes available for new lock acquisitions
