# Change: Update local schema pack bootstrap + validation

## Why
Today `aos validate schemas` validates an engine-embedded schema pack, but the desired operating model is that **each target repo owns its validation schema pack under `.aos/schemas/**`**, created by `aos init` as deterministic default templates that the orchestrator/agents can evolve per project.

## What Changes
- `aos init` writes a **local schema pack** under `.aos/schemas/**`:
  - `.aos/schemas/registry.json` becomes non-empty and enumerates the schema files.
  - `.aos/schemas/*.schema.json` are materialized deterministically from engine-owned embedded templates.
- `aos init` also writes deterministic default spec templates under `.aos/spec/**` (including `project.json` and `roadmap.json`).
- `aos validate schemas` is updated to validate the **local schema pack** under `.aos/schemas/**` (requires `aos init` first).

## Impact
- **Affected specs**:
  - `aos-schema-registry` (local vs embedded validation contract)
  - `aos-workspace-bootstrap` (init seeds local schema pack files)
- **Affected code**:
  - `Gmsd.Aos/Engine/Workspace/AosWorkspaceBootstrapper.cs`
  - `Gmsd.Aos/Composition/Program.cs`
  - `Gmsd.Aos/Engine/Schemas/*` (local loader for `.aos/schemas/**`)

