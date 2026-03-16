## ADDED Requirements
### Requirement: Error Code Catalog
The system SHALL provide a centralized set of error code constants for shared use across layers.

#### Scenario: Shared error codes
- **WHEN** a service or API component needs an error identifier
- **THEN** it uses a common constant rather than a hard-coded literal

### Requirement: Clock Abstraction
The system SHALL provide an `IClock` abstraction with a default `SystemClock` implementation.

#### Scenario: Consistent time source
- **WHEN** code requires the current time
- **THEN** it requests the time from `IClock` in UTC

### Requirement: Common Exceptions
The system SHALL provide common exception types that carry an error code for consistent API translation.

#### Scenario: Not found exception
- **WHEN** a requested entity is missing
- **THEN** the code throws a common not-found exception with an error code

### Requirement: Paging Primitives
The system SHALL provide paging primitives for list endpoints (`PageRequest`, `PageResult<T>`).

#### Scenario: Paged response
- **WHEN** a list endpoint returns results
- **THEN** it uses the paging primitives with total count and page metadata
