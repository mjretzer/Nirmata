# Change: Update spec store CRUD + indexing (PH-ENG-0004)

## Why
PH-ENG-0004 requires deterministic CRUD APIs for spec artifacts under `.aos/spec/**` plus stable catalog indexes. The engine already has deterministic JSON writing, index maintenance helpers, and index repair, but SpecStore CRUD is currently incomplete (project/roadmap only) and Issue/UAT contract paths conflict with the desired workspace output shape.

## What Changes
- Add SpecStore CRUD support for spec artifacts:
  - project, roadmap
  - milestones, phases, tasks, issues, uat
  - task sub-artifacts: `.aos/spec/tasks/<id>/plan.json` and `.aos/spec/tasks/<id>/links.json`
- Ensure create/delete operations update catalog indexes deterministically:
  - milestones/phases/tasks/issues/uat `index.json` files
- **BREAKING**: Update canonical routing for Issue and UAT artifact contract paths to flat files:
  - `.aos/spec/issues/ISS-####.json`
  - `.aos/spec/uat/UAT-####.json`
  (replacing folder-based paths like `.aos/spec/issues/ISS-####/issue.json` and `.aos/spec/uat/UAT-####/uat.json`)
- Update index repair to discover Issue/UAT artifacts using the new flat file layout.
- Extend tests so create/update/show/list flows pass and “delete index then rebuild” produces deterministic, stable entries.

## Impact
- **Affected OpenSpec capabilities**:
  - `aos-spec-store`
  - `aos-path-routing`
  - `aos-index-repair`
  - `aos-workspace-validation` (adds explicit coverage scenarios for Issue/UAT contract paths)
- **Affected code (expected)**:
  - `Gmsd.Aos/Engine/Stores/AosSpecStore.cs`
  - `Gmsd.Aos/Engine/Paths/AosPathRouter.cs`
  - `Gmsd.Aos/Engine/Repair/AosIndexRepairer.cs`
  - `Gmsd.Aos/Engine/Validation/AosWorkspaceValidator.cs` (as needed for updated contract path expectations)
  - `tests/Gmsd.Aos.Tests/**` (routing + repair determinism + CRUD flows)
- **Migration / compatibility**:
  - Existing workspaces using the legacy folder-based Issue/UAT layouts must be migrated to the flat file layout to be considered canonical by routing and any routing-based validation.
  - `aos repair indexes` is expected to rebuild indexes deterministically for the canonical layout; it does not automatically move/migrate legacy artifacts unless explicitly extended to do so.

