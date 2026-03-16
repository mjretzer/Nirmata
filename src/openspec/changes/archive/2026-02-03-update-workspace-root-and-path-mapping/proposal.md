# Change: Update workspace root and canonical path mapping

## Why
The engine already enforces additional canonical workspace folders and validation behaviors (notably `config/`, `locks/`, and a `config` validation layer) that are not yet reflected in the specifications. This drift makes it hard for consumers and contributors to know the authoritative `.aos/*` contract and how to discover the repository root deterministically.

## What Changes
- Align the `.aos/` canonical workspace contract with current engine behavior by including `.aos/config/` and `.aos/locks/` as first-class top-level folders.
- Extend workspace validation requirements to include the `config` layer as a selectable and default-validated layer (while keeping config artifacts optional unless present).
- Extend path routing requirements beyond ID-based spec artifacts to include non-ID canonical contract paths (e.g., workspace lock) and contract-path invariants.
- Expand the public compile-against surface so consumers can discover repository root + `.aos/` root and resolve canonical contract paths without referencing internal engine namespaces.

## Impact
- **Affected specs**:
  - `aos-workspace-bootstrap`
  - `aos-workspace-validation`
  - `aos-path-routing`
  - `aos-public-api-surface`
- **Affected code** (expected during apply stage):
  - `nirmata.Aos/Composition/Program.cs` (repo root discovery behavior)
  - `nirmata.Aos/Engine/Workspace/**` (bootstrap contract alignment)
  - `nirmata.Aos/Engine/Validation/**` (layer naming + defaults)
  - `nirmata.Aos/Engine/Paths/AosPathRouter.cs` (canonical contract paths)
  - `nirmata.Aos/Public/IWorkspace.cs` (public surface expansion)

