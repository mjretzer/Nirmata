# Spec Delta: Robust Error Handling

## MODIFIED Requirements

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
