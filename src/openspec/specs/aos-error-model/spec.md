# aos-error-model Specification

## Purpose

Defines canonical AOS workspace contracts and behavioral semantics for $capabilityId.

- **Lives in:** `nirmata.Aos/*`, `.aos/**`
- **Owns:** Engine-level artifact contracts, validation, and deterministic IO semantics for this capability
- **Does not own:** Plane/orchestrator workflows (owned by `agents-*` capabilities)
## Requirements
### Requirement: Diagnostic logging for non-critical "best-effort" failures
The system SHALL provide diagnostic visibility into "best-effort" operations (e.g., cache cleanup, temporary file removal) that would otherwise be silently swallowed.

The implementation MUST:
- Replace empty `catch { }` blocks with structured diagnostic logging.
- Use appropriate log levels (e.g., `Warning` for failed cleanup, `Error` for unexpected failures in critical paths).
- Include context in logs, such as the target file path, operation attempted, and the exception message.

#### Scenario: Failed cache cleanup is logged as a warning
- **GIVEN** a `CacheManager` attempting to delete a locked file
- **WHEN** the `IOException` occurs
- **THEN** the system catches the exception
- **AND** it emits a `Warning` log entry describing the failure and the file path
- **AND** it continues with the remaining cleanup tasks

### Requirement: Consistent resource disposal and exception safety
The system SHALL ensure that all system resources (file handles, streams, JSON documents) are deterministically disposed of, even in the event of an exception.

The implementation MUST:
- Use `using` statements or `try-finally` blocks for all `IDisposable` resources.
- Ensure that partial failures during multi-step operations (e.g., `AppendEvent`) do not leave the system in an inconsistent state or with leaked resources.

#### Scenario: Exception during event append does not leak file handle
- **GIVEN** an `AosStateStore` appending an event to the log
- **WHEN** an exception occurs during the write operation
- **THEN** the `FileStream` is immediately closed and disposed
- **AND** the exception is propagated or handled according to the orchestrator's error model

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

