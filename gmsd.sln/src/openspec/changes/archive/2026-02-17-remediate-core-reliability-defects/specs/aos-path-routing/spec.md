# Spec Delta: Path Sanitization

## MODIFIED Requirements

### Requirement: All AOS artifact paths MUST be validated against the workspace root
The system SHALL enforce a "jail" policy for all path resolution, ensuring that no artifact path can escape the `.aos` directory or the workspace root.

The implementation MUST:
- Reject any path containing directory traversal segments (e.g., `..`, `./..`).
- Validate that the resolved absolute path is a child of the workspace root.
- Normalize all paths to use forward slashes (`/`) for platform neutrality before validation.

#### Scenario: Malicious path traversal is blocked
- **GIVEN** a request to resolve a path like `.aos/spec/../../secret.txt`
- **WHEN** the path router resolves the artifact path
- **THEN** the system throws a `ValidationFailedException` or returns a failure result
- **AND** no file access is attempted outside the workspace root

### Requirement: Centralized path resolution for all stores
All internal stores (SpecStore, StateStore, CacheManager, EvidenceStore) SHALL obtain file paths exclusively through the `AosPathRouter`.

#### Scenario: Cache operations are scoped to the cache directory
- **GIVEN** a `CacheManager` instance
- **WHEN** any cache operation is performed
- **THEN** it uses paths resolved and validated by `AosPathRouter`
- **AND** it is physically impossible to delete files in `.aos/spec/` via cache cleanup methods
