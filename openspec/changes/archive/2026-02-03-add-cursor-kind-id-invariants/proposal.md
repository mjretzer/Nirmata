# Change: Add cursor kind/id invariants

## Why
PH-ENG-0003 requires deterministic cross-file invariants for references between `.aos/**` artifacts. The workspace validator already enforces roadmap references, but the state cursor remains schema-light and has no deterministic reference validation.

This change standardizes a minimal cursor reference shape and adds invariants so a malformed or dangling cursor fails validation before workflows proceed.

## What Changes
- Define a minimal cursor reference shape: optional `cursor.kind` + `cursor.id` in `.aos/state/state.json`.
- Add deterministic workspace invariants for cursor references:
  - when `cursor.kind` + `cursor.id` are present, they must be consistent, parseable, and resolve to an existing artifact at the canonical contract path.
  - when a catalog index exists for the referenced kind, the cursor id must be present in the index.

## Impact
- **Affected specs**:
  - `openspec/specs/aos-state-store/spec.md`
  - `openspec/specs/aos-workspace-validation/spec.md`
- **Affected code (planned in apply stage)**:
  - `Gmsd.Aos/Resources/Schemas/state-snapshot.schema.json`
  - `Gmsd.Aos/Engine/Validation/AosWorkspaceValidator.cs`
  - tests under `tests/Gmsd.Aos.Tests/`

