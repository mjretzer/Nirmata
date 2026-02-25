# aos-deterministic-json-serialization Specification

## Purpose

Defines canonical AOS workspace contracts and behavioral semantics for $capabilityId.

- **Lives in:** `Gmsd.Aos/*`, `.aos/**`
- **Owns:** Engine-level artifact contracts, validation, and deterministic IO semantics for this capability
- **Does not own:** Plane/orchestrator workflows (owned by `agents-*` capabilities)
## Requirements
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

### Requirement: Deterministic JSON writes avoid churn via streaming comparison
When writing JSON via the canonical deterministic JSON writer, the system MUST avoid rewriting the file when the canonical serialized bytes are already identical to the existing file bytes. For large files, this comparison MUST be performed using a streaming approach to minimize memory overhead.

The implementation MUST:
- Compare the existing file content with the new canonical representation in chunks (e.g., 4KB).
- Halt comparison and trigger a write as soon as the first byte mismatch is detected.
- Avoid loading the entire existing file or the new canonical representation into a single contiguous memory buffer for comparison.

#### Scenario: Identical large files incur minimal memory overhead
- **GIVEN** a 50MB JSON artifact at the target path
- **AND** a new canonical representation that is byte-identical to the existing file
- **WHEN** the deterministic JSON writer is invoked
- **THEN** the system compares the contents in chunks
- **AND** the peak memory usage remains significantly lower than the file size
- **AND** the file is not rewritten

#### Scenario: Large file with early mismatch halts comparison
- **GIVEN** a 50MB JSON artifact
- **AND** a new canonical representation that differs in the first 1KB
- **WHEN** the deterministic JSON writer is invoked
- **THEN** the mismatch is detected within the first chunk
- **AND** the system immediately proceeds to atomic overwrite without reading the remainder of the existing file

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

