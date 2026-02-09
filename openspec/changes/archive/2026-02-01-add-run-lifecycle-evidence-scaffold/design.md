## Context
This milestone introduces the minimal “run lifecycle” shell (start/finish) and the canonical evidence folder layout under `.aos/evidence/` that later workflows will populate.

It intentionally avoids planning/execution semantics; it only establishes run identity and evidence structure.

## Goals / Non-Goals
### Goals
- Provide `aos run start` and `aos run finish`.
- Create a deterministic run evidence folder at a canonical path under `.aos/evidence/runs/<run-id>/`.
- Create a deterministic run metadata index under `.aos/evidence/runs/`.
- Establish stable filenames and JSON formatting conventions for run metadata.

### Non-Goals
- Running tasks, generating plans, or executing workflows.
- Validating run evidence via `aos validate workspace` (may be added in a later milestone).
- Rich logging/streaming; this milestone only scaffolds folders/files.

## Decisions
### Decision: Run IDs are opaque, unique identifiers
Run IDs MUST be unique and safe for folder names (ASCII, no path separators). The implementation may use GUIDs (recommended: `N` format) to avoid new dependencies.

### Decision: Determinism applies to structure + formatting, not “time”
Run creation records inherently non-deterministic facts (timestamps, machine/user). “Deterministic” in this milestone means:
- canonical folder/file names
- stable JSON formatting (UTF-8, LF, stable key ordering where applicable)
- predictable index/update behavior

### Decision: Evidence folder layout
Proposed minimal contract:
- `.aos/evidence/runs/index.json` (run index)
- `.aos/evidence/runs/<run-id>/run.json` (run metadata)
- `.aos/evidence/runs/<run-id>/logs/` (scaffold)
- `.aos/evidence/runs/<run-id>/outputs/` (scaffold)

### Decision: Evidence logic code location
Evidence-writing logic for this milestone SHOULD live under `Gmsd.Aos/Engine/Evidence/` (namespace `Gmsd.Aos.Engine.Evidence`) to keep `.aos/evidence/**` filesystem concerns inside the engine layer.

## Risks / Trade-offs
- Without an explicit schema for run artifacts, later validation and tooling may drift → mitigate by adding JSON schemas for run artifacts in a follow-up milestone once the contract stabilizes.

## Open Questions
- Do we want `aos run finish` to require an explicit `--run-id` (recommended) or infer an “active run” from state?
- Should the index be append-only (audit-friendly) or allow updates (e.g., status transitions)?

