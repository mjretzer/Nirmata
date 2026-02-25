## 1. Specification updates
- [x] 1.1 Add new capability delta spec `aos-schema-registry` (embedded schema pack + canonical naming).
- [x] 1.2 Add new capability delta spec `aos-workspace-validation` (CLI + schema validation + invariants).

## 2. Engine implementation: schema registry
- [x] 2.1 Add embedded JSON Schema assets for the AOS workspace artifacts validated in this milestone.
- [x] 2.2 Implement a schema registry loader that enumerates/loads embedded schemas deterministically.
- [x] 2.3 Enforce canonical schema filenames for embedded schema assets (reject non-canonical).
- [x] 2.4 Implement `aos validate schemas` (human-readable output + non-zero exit code on failure).

## 3. Engine implementation: workspace validation
- [x] 3.1 Implement a workspace validator that can validate selected layers (`spec|state|evidence|codebase|context`).
- [x] 3.2 Implement invariant checks (single-project model + roadmap constraints) and fail fast on breach.
- [x] 3.3 Implement `aos validate workspace [--layers ...]` (defaults to all layers).

## 4. Tests
- [x] 4.1 Add tests that prove `aos validate schemas` detects malformed schemas and non-canonical schema filenames.
- [x] 4.2 Add tests that prove `aos validate workspace` detects malformed JSON in workspace artifacts.
- [x] 4.3 Add tests for invariants:
  - missing `.aos/spec/project.json`
  - forbidden `.aos/spec/projects.json`
  - forbidden `.aos/state/active-project.json`
  - invalid multi-project reference in `.aos/spec/roadmap.json`

## 5. Developer ergonomics
- [x] 5.1 Update CLI help output to include new validation commands and options.
- [x] 5.2 Ensure CI runs the new validation tests.

