# Change: Add schema-based workspace validation

## Why
The engine needs to validate `.aos/**` artifacts structurally and deterministically **before** workflows proceed. Today `aos validate workspace` primarily checks presence and “well-formed JSON”, which is not sufficient to detect schema violations or produce consistent machine-readable reports.

This change advances PH-ENG-0003 by making validation schema-based, standardizing schema identification by JSON Schema `$id`, and adding deterministic cross-file invariants for roadmap references.

## What Changes
- Make `aos validate workspace` validate known artifacts **against the local schema pack** (not just JSON parsing).
- Treat JSON Schema `$id` as the canonical schema identifier and load embedded schemas deterministically by `$id`.
- Expand the engine-owned schema pack to cover baseline artifacts written by `aos init` and upcoming spec artifacts.
- Add cross-file invariant checks for `.aos/spec/roadmap.json` item references:
  - `kind` is recognized
  - `id` parses and matches the kind
  - referenced artifact exists at the canonical contract path
  - if a catalog index exists for the kind, the referenced id is present in the index
- Emit normalized, machine-readable validation issues that include contract path, schema id, and instance location.

## Impact
- **Affected specs**:
  - `openspec/specs/aos-schema-registry/spec.md`
  - `openspec/specs/aos-workspace-validation/spec.md`
- **Affected code (planned)**:
  - `Gmsd.Aos/Engine/Schemas/**` (schema registry by `$id`)
  - `Gmsd.Aos/Engine/Validation/**` (schema-based validation + normalized reporting)
  - `Gmsd.Aos/Public/Catalogs/SchemaIds.cs` and `Gmsd.Aos/Public/Catalogs/ArtifactKinds.cs` (fill stubs)
  - `Gmsd.Aos/Resources/Schemas/**` (additional `*.schema.json`)
- **Dependencies**:
  - Introduces a JSON Schema validator library dependency (see `design.md`).

