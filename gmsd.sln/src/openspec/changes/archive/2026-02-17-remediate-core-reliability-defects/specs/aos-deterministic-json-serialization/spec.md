# Spec Delta: Deterministic JSON Streaming

## MODIFIED Requirements

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
