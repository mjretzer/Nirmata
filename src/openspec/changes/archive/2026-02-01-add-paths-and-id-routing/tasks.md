## 1. Specification updates
- [x] 1.1 Add new capability delta spec `aos-path-routing` defining ID parsing and deterministic ID → path routing.

## 2. Engine implementation: routing source of truth
- [x] 2.1 Introduce a single routing component (e.g., `AosPathRouter`) that maps IDs (RUN/MS/PH/TSK/ISS/UAT) to contract paths under `.aos/*`.
- [x] 2.2 Enforce ID validation rules:
  - RUN: 32-char lower-hex GUID (current engine format)
  - MS/PH/TSK/ISS/UAT: prefix + zero-padded numeric (per spec)
- [x] 2.3 Update all engine/CLI code that currently builds `.aos/*` paths ad-hoc to call the router instead.

## 3. Tests
- [x] 3.1 Add unit tests that prove every supported artifact ID resolves to exactly one deterministic contract path.
- [x] 3.2 Add negative tests proving invalid IDs are rejected with actionable diagnostics.
- [x] 3.3 Add regression tests proving routing does not differ across platforms (path separators, casing rules).

## 4. Developer ergonomics
- [x] 4.1 Add developer docs/notes (if needed) describing the routing rules and how new commands should use the router.
- [x] 4.2 Ensure CI runs routing tests.

