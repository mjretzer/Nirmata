## ADDED Requirements
### Requirement: Canonical deterministic JSON writer exists for AOS artifacts
The system SHALL provide a canonical deterministic JSON writer for **AOS-emitted** JSON artifacts under `.aos/**`.

The canonical deterministic JSON writer MUST:
- emit UTF-8 without BOM
- emit LF (`\n`) line endings
- emit stable formatting (indentation and whitespace) so byte-for-byte comparison is possible
- emit a trailing newline at end-of-file
- not depend on OS, current culture, machine settings, or filesystem scanning for output determinism

#### Scenario: Canonical writer produces deterministic JSON bytes
- **GIVEN** an in-memory JSON value representing an AOS artifact
- **WHEN** the value is written using the canonical deterministic JSON writer
- **THEN** the resulting bytes are deterministic across runs and hosts for identical inputs

### Requirement: JSON object keys are written in canonical order recursively
When writing JSON via the canonical deterministic JSON writer, the system MUST canonicalize JSON objects by ordering all object keys using **ordinal string ordering**.

This ordering MUST be applied recursively to every nested JSON object, regardless of the originating in-memory type (e.g., POCOs, dictionaries, dynamic JSON nodes).

#### Scenario: Nested JSON object keys are sorted deterministically
- **GIVEN** an input JSON value that contains nested objects with keys in arbitrary order
- **WHEN** the value is written using the canonical deterministic JSON writer
- **THEN** every JSON object in the output has its keys ordered using ordinal string ordering

### Requirement: Deterministic JSON writes are atomic and safe
When writing JSON via the canonical deterministic JSON writer, the system MUST write files atomically to prevent partial/corrupt artifacts.

The writer MUST:
- write to a temporary file in the same directory as the target file
- commit the write via an atomic replace/move
- guarantee that a failed write does not leave a partially-written target file
- best-effort delete any temporary files left behind

#### Scenario: Deterministic JSON write does not leave partial artifacts
- **GIVEN** an existing valid JSON file at the target path
- **WHEN** the canonical deterministic JSON writer writes an updated value to that path
- **THEN** the write commits atomically and the target path never contains partial/corrupt JSON at any time

### Requirement: Deterministic JSON writes avoid churn
When writing JSON via the canonical deterministic JSON writer, the system MUST avoid rewriting the file when the canonical serialized bytes are already identical to the existing file bytes.

#### Scenario: Identical canonical content is not rewritten
- **GIVEN** a target JSON file whose contents are identical to the canonical serialization of the intended value
- **WHEN** the canonical deterministic JSON writer is invoked for that target
- **THEN** the target file is not rewritten
