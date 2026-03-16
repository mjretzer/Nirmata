## Context
Current planning/execution artifacts have drifted contract interpretations across components (for example, `fileScopes` as either strings or objects, and mixed path field names). This causes scope enforcement failures, inconsistent verification behavior, and unstable UI rendering.

The repo already has strong primitives for schema registry, deterministic JSON writing, and workspace validation, but runtime artifact producers/consumers still permit divergent shapes in key workflow artifacts.

## Goals / Non-Goals
- Goals:
  - Define one canonical schema per workflow artifact type.
  - Define a schema version policy that enables deterministic reader behavior.
  - Align typed models and serializer/deserializer boundaries to those canonical schemas.
  - Enforce schema validation on read and emit friendly diagnostics on failure.
- Non-Goals:
  - Redesign unrelated product-domain DTOs.
  - Introduce non-JSON artifact formats.
  - Build a broad migration framework beyond the specific affected artifact contracts.

## Decisions
- Decision: Treat schema `$id` + artifact `schemaVersion` as the canonical contract identity.
  - Why: `$id` identifies artifact type, while `schemaVersion` controls compatibility and migration semantics.

- Decision: Standardize task-plan `fileScopes` as object entries with `path` as the canonical path property.
  - Why: object entries support deterministic expansion (permissions, rationale) without shape ambiguity.

- Decision: Require all writers to produce schema-valid deterministic JSON, and all readers to validate before use.
  - Why: this removes parser-specific drift and prevents invalid artifacts from flowing downstream.

- Decision: Validation failures must produce diagnostic artifacts consumable by humans and UI.
  - Why: failures become observable and actionable instead of opaque parser errors.

## Risks / Trade-offs
- Risk: Existing artifacts in old shapes may fail immediately.
  - Mitigation: permit explicit supported-version list and deterministic diagnostics that state expected version and shape.

- Risk: Cross-cutting updates affect multiple planner/executor/verifier paths.
  - Mitigation: centralize typed contract usage and validation helpers instead of per-component ad-hoc parsing.

## Migration Plan
1. Introduce/confirm canonical schema entries and version policy in schema registry metadata.
2. Update artifact writers to emit canonical shape and include required schema version fields.
3. Update readers to validate on read and return diagnostic artifacts when invalid.
4. Add compatibility tests for expected prior versions (if retained) and rejection tests for unsupported versions.

## Open Questions
- Should unsupported legacy versions be hard-fail only, or support a bounded auto-upgrade path for specific artifacts?
- What is the canonical diagnostic artifact path convention for runtime validation failures across planner/executor/verifier?

## Canonical Artifact Contract Inventory
Each artifact type MUST map to exactly one canonical schema identity (`$id`) and one current `schemaVersion`.

| Artifact type | Artifact path/pattern | Canonical schema `$id` | Current `schemaVersion` |
| --- | --- | --- | --- |
| Task plan | `.aos/spec/tasks/<task-id>/plan.json` | `nirmata.task-plan` | `1` |
| Fix planning output (`plan.json`) | `.aos/spec/tasks/<task-id>/plan.json` (fix planning writer) | `nirmata.task-plan` | `1` |
| Command proposal artifact | runtime command-proposal payload/artifact boundary | `nirmata.command-proposal` | `1` |

Notes:
- Task planning and fix planning both emit `plan.json` and therefore share the same canonical task-plan contract.
- If fix planning introduces a distinct artifact in the future, it MUST receive its own new schema `$id`.

## Schema Versioning and Compatibility Policy
- Version fields are integer `schemaVersion` values.
- `current version` = the version all writers MUST emit (`1` for this change).
- `supported versions` = versions readers MAY accept for a given schema `$id`.
- Writers MUST emit only the current version and MUST NOT emit deprecated versions.
- Readers MUST reject unsupported versions deterministically and include:
  - artifact path
  - schema `$id`
  - declared `schemaVersion`
  - supported versions
  - actionable remediation text
- Deprecation behavior:
  1. Mark an older version as deprecated in registry metadata while still listed as supported.
  2. Emit a warning diagnostic when deprecated versions are read.
  3. Remove deprecated versions from `supported versions` only in a later explicit change.

## Canonical `fileScopes` Shape and Path Naming
Task-plan artifacts MUST encode `fileScopes` as object entries with `path` as the only canonical path property name.

Canonical shape:

```json
{
  "schemaVersion": 1,
  "fileScopes": [
    {
      "path": "nirmata.Agents/Execution/PlanWriter.cs"
    }
  ]
}
```

Rules:
- `fileScopes` MUST be an array of objects (not strings).
- `fileScopes[].path` MUST be a workspace-relative path.
- Alternate names like `filePath`, `relativePath`, or bare string entries are out-of-contract.
