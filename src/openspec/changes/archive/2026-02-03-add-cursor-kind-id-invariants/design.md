## Context
The workspace state snapshot (`.aos/state/state.json`) is intentionally schema-light in v1. However, the engine still needs deterministic invariants for cross-file references so invalid state cannot silently route workflows.

Roadmap references already validate `kind` + `id` pairs deterministically. This change adopts the same approach for the state cursor.

## Goals / Non-Goals
- Goals:
  - Provide a minimal, stable cursor reference surface: `cursor.kind` + `cursor.id`.
  - Enforce deterministic cursor reference validation in `aos validate workspace`.
- Non-Goals:
  - Define a complete hierarchical cursor schema (milestoneId/phaseId/taskId).
  - Require the cursor reference fields to exist in all workspaces (they are optional in v1).

## Decisions
- **Decision: Minimal cursor reference uses kind/id.**
  - `cursor.kind`: stable string aligned to `nirmata.Aos.Public.Catalogs.ArtifactKinds`
  - `cursor.id`: artifact identifier; parsing and canonicalization aligned to routing rules
- **Decision: Invariants are conditional.**
  - If both `cursor.kind` and `cursor.id` are present, invariants apply.
  - If only one is present, validation fails (cursor is malformed).

## Validation rules (cursor.kind + cursor.id)
- kind MUST be recognized
- id MUST parse as that kind and be canonical
- referenced artifact MUST exist at the canonical contract path
- if a catalog index exists for the kind, the id MUST be present in that index

## Error reporting
Validation failures should be reported deterministically with:
- `contractPath`: `.aos/state/state.json`
- `instanceLocation`: JSON Pointer-like location (e.g., `/cursor/kind`, `/cursor/id`)
- an actionable message (including expected kinds where relevant)

