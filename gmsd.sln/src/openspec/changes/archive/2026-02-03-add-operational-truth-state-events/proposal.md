# Change: Add operational truth state + events

## Why
The engine needs a single, durable “operational truth” for progress and resumability that can be validated, replayed, and queried deterministically from disk.

Today, `.aos/state/state.json` is intentionally schema-light and `.aos/state/events.ndjson` is append-only, but there is no spec-defined cursor model, no deterministic derivation rules, and no stable tail/filter contract for consuming the event stream.

## What Changes
- Define a **concrete cursor model** in `.aos/state/state.json` that captures progress across milestone/phase/task/step with stable status values.
- Define a deterministic **state reducer**: applying the same ordered events to the same baseline yields byte-identical `state.json` (canonical JSON, no churn).
- Define **tail + filter semantics** for `.aos/state/events.ndjson` so consumers can read stable ordered slices without scanning the entire file.
- Tighten workspace validation for `.aos/state/events.ndjson`:
  - non-empty lines MUST be JSON objects
  - each event line MUST validate against the local event schema (`gmsd:aos:schema:event:v1`), while keeping that schema permissive (only `schemaVersion` required).
- **BREAKING (engine-internal contract)**: cursor shape in `state.json` becomes structurally meaningful (no longer treated as an opaque blob).

## Impact
- **Specs**:
  - `openspec/specs/aos-state-store/spec.md` (cursor model, reducer determinism, tail/filter semantics)
  - `openspec/specs/aos-workspace-validation/spec.md` (events.ndjson object-per-line + schema validation)
  - `openspec/specs/aos-public-api-surface/spec.md` (define real `IStateStore` public contract + stable state contract DTOs)
- **Code paths (implementation stage)**:
  - `Gmsd.Aos/Engine/Stores/AosStateStore.cs`
  - `Gmsd.Aos/Engine/Validation/AosWorkspaceValidator.cs`
  - `Gmsd.Aos/Resources/Schemas/state-snapshot.schema.json`
  - `Gmsd.Aos/Public/**` and `Gmsd.Aos/Contracts/**`
- **Tests (implementation stage)**:
  - `tests/Gmsd.Aos.Tests/**` (state store determinism, event tail/filter, workspace validation)

