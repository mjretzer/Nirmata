# Change: Add context pack builder (PH-ENG-0007)

## Why
AOS needs a deterministic, budgeted way to assemble bounded “run input” from an AOS workspace, without depending on ad-hoc file reads or unbounded repository context. Context packs provide a stable, schema-validated, self-contained input bundle for task/phase execution.

## What Changes
- Add a new spec capability `aos-context-packs` defining:
  - context pack artifact shape (self-contained contents + metadata)
  - deterministic packing for task-mode and phase-mode
  - budget enforcement and deterministic selection order
- Extend `aos-path-routing` to treat context packs as a first-class routed artifact:
  - new ID format `PCK-####`
  - canonical pack path `.aos/context/packs/PCK-####.json`
- Extend `aos-workspace-validation` so `aos validate workspace` schema-validates context packs when present.

## Impact
- **Affected specs**:
  - New: `openspec/specs/aos-context-packs/spec.md` (introduced via change delta)
  - Modified: `openspec/specs/aos-path-routing/spec.md`
  - Modified (additive): `openspec/specs/aos-workspace-validation/spec.md`
- **Affected code (expected in apply stage)**:
  - Routing: `nirmata.Aos/Engine/Paths/AosPathRouter.cs`
  - Context pack schema: `nirmata.Aos/Resources/Schemas/context-pack.schema.json`
  - Workspace validation: `nirmata.Aos/Engine/Validation/AosWorkspaceValidator.cs`
  - Context pack builder/writer: `nirmata.Aos/Context/**`, `nirmata.Aos/Context/Packs/**`
  - CLI surface: `nirmata.Aos/Composition/Program.cs`

