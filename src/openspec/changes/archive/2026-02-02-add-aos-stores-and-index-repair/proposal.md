# Change: Add AOS workspace stores and deterministic index repair

## Why
Milestone E1 requires the `.aos/*` workspace contract to be enforceable and self-healing. Today we have deterministic routing, deterministic JSON IO, and schema filename enforcement, but we do not yet have first-class stores (spec/state/evidence) or deterministic index repair—so the workspace cannot be reliably validated, repaired, or reconstructed from disk alone.

## What Changes
- Add first-class **workspace stores**:
  - **Spec store** for `.aos/spec/**` artifacts and catalog indexes.
  - **State store** for `.aos/state/state.json` and append-only `.aos/state/events.ndjson`.
  - **Evidence store** for standardized evidence logs and run manifests.
- Add a deterministic **index repair** CLI surface: `aos repair indexes`.
- Extend `aos init` to seed additional baseline artifacts required for strict validation.
- Extend `aos validate workspace` to validate the new required artifacts and provide actionable guidance (including pointing to repair where appropriate).

## Non-Goals
- Implementing the full planning/execution workflow state machine (beyond minimal state/evidence persistence contracts).
- Adding full JSON Schema coverage for every new artifact type in this change (we will validate structural JSON correctness and deterministic writing; schema completeness can follow once artifacts stabilize).
- Changing product-domain persistence (EF Core / SQLite) or product APIs/UI.

## Impact
- **Affected specs**:
  - new: `aos-spec-store`, `aos-state-store`, `aos-evidence-store`, `aos-index-repair`
  - modified: `aos-workspace-bootstrap`, `aos-workspace-validation`
- **Affected code** (expected during implementation):
  - `nirmata.Aos` (engine workspace contracts, CLI command handlers, stores, validation)
  - `tests/nirmata.Aos.Tests` (new deterministic fixtures + contract tests)

