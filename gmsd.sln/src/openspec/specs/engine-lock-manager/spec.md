# engine-lock-manager Specification

## Purpose

Defines the public DI/interface surface for acquiring/releasing and inspecting the exclusive AOS workspace lock. Canonical locking semantics and CLI behavior are defined by `aos-lock-manager`, and this capability MUST conform.

- **Lives in:** `Gmsd.Aos/Public/ILockManager.cs`, `Gmsd.Aos/Public/Composition/*`, `.aos/locks/workspace.lock.json`
- **Owns:** Public interface shape and DI registration for lock operations
- **Does not own:** Stable exit codes, command gating, or OS-level locking strategy (owned by canonical lock spec)
## Requirements
### Requirement: Lock manager interface exists
The system SHALL define `ILockManager` as a public interface in `Gmsd.Aos/Public/`.

The interface SHALL provide methods to acquire, release, and inspect the exclusive workspace lock.

#### Scenario: Acquire lock creates lock file
- **GIVEN** an unlocked workspace
- **WHEN** `ILockManager.Acquire()` is called
- **THEN** the lock file is created at `.aos/locks/workspace.lock.json`
- **AND** the call returns true

#### Scenario: Acquire lock fails fast when already held
- **GIVEN** a locked workspace
- **WHEN** `ILockManager.Acquire()` is called
- **THEN** the call returns false immediately (no unbounded waiting)

#### Scenario: Acquire lock includes holder information
- **GIVEN** a locked workspace
- **WHEN** `ILockManager.Acquire()` is called and fails
- **THEN** the lock file content identifies the holder (process info, timestamp)

#### Scenario: Release lock removes lock file
- **GIVEN** a locked workspace
- **WHEN** `ILockManager.Release()` is called
- **THEN** the lock file is removed
- **AND** the call returns true

#### Scenario: Release lock without holding fails
- **GIVEN** an unlocked workspace
- **WHEN** `ILockManager.Release()` is called
- **THEN** the call returns false

#### Scenario: Release with force bypasses validation
- **GIVEN** a workspace with a corrupted/unparseable lock file
- **WHEN** `ILockManager.Release(force: true)` is called
- **THEN** the lock file is removed
- **AND** the call returns true

#### Scenario: Get status returns lock state
- **GIVEN** any workspace state
- **WHEN** `ILockManager.GetStatus()` is called
- **THEN** the lock state is returned (locked/unlocked + holder info if locked)

### Requirement: Lock file format is actionable
The interface SHALL write lock files with actionable content.

#### Scenario: Lock file contains holder details
- **GIVEN** a successfully acquired lock
- **WHEN** the lock file is read
- **THEN** it contains: processId, processName, startedAtUtc, machineName

### Requirement: Service is registered in DI
The system SHALL register `ILockManager` as a Singleton in `AddGmsdAos()`.

#### Scenario: Plane resolves the service via DI
- **GIVEN** a configured service collection with `AddGmsdAos()` called
- **WHEN** `serviceProvider.GetRequiredService<ILockManager>()` is called
- **THEN** a non-null implementation is returned

