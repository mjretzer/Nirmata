# Change: Roadmap Modifier / Phase Remover Workflows

## Why

Planning workflows need the ability to modify roadmaps after initial creation. Teams need to insert new phases mid-stream, remove obsolete phases, and maintain consistent numbering. Without safe modification operations, roadmap changes risk corrupting cursor state or losing execution context.

## What Changes

- **ADDED**: `IRoadmapModifier` interface and implementation for safe roadmap modifications
- **ADDED**: Phase insertion with automatic renumbering
- **ADDED**: Phase removal with safety checks (active phase protection)
- **ADDED**: `IRoadmapRenumberer` for consistent phase ID sequencing
- **ADDED**: Issue creation when phase removal is blocked
- **ADDED**: Cursor coherence preservation during all modifications
- **ADDED**: `RoadmapModifierHandler` for orchestrator integration

## Impact

- **Affected specs**: `phase-planning` (modifications), `scope-management` (new capability)
- **Affected code**: `Gmsd.Agents/Execution/Planning/RoadmapModifier/**`
- **Breaking changes**: None (additive only)
