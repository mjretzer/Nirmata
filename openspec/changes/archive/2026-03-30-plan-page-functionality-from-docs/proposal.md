## Why

The current Plan page is partially driven by synthetic fallback content and local UI-only state, which can diverge from canonical AOS artifacts and workflows documented in this repository. We need the Plan page to reflect documented planning behavior and artifact structure so users can reliably inspect and navigate real planning data.

## What Changes

- Replace synthetic/fallback Plan page artifact rendering with workspace-backed artifact loading and documented path semantics under `.aos/spec`.
- Enforce deterministic route-to-lens mapping for root, phase directory, and artifact file paths in the Plan page.
- Define explicit loading, empty, missing-artifact, and error behavior for all Plan page lenses.
- Ensure Plan page task/phase interactions are derived from canonical workspace data and never from generated substitute artifact payloads.

## Capabilities

### New Capabilities
- `plan-page-lens`: Define Plan page requirements for rendering, navigation, and artifact-backed behavior from documented AOS planning artifacts.

### Modified Capabilities
- `workspace-domain-data`: Clarify frontend-consumed behavior for workspace file/spec reads used by plan lenses, including missing-artifact handling and artifact path expectations.

## Impact

- Affected frontend areas: Plan page and plan components in `nirmata.frontend/src/app/pages` and `nirmata.frontend/src/app/components/plan`.
- Affected frontend data hooks: `nirmata.frontend/src/app/hooks/useAosData.ts` file/spec loading paths consumed by plan views.
- Affected API contract usage: workspace-scoped spec/files endpoints already defined under workspace domain data.
- No intended breaking public API path changes; behavior alignment is focused on correctness and deterministic UX.
