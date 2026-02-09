## Context
PH-ENG-0007 introduces **context packs** as deterministic, budgeted bundles of bounded inputs for AOS runs.

This change treats context packs as:
- **First-class routed artifacts** via a stable pack ID format `PCK-####`.
- **Self-contained** bundles: pack entries embed the relevant artifact contents (JSON/text) plus metadata needed for verification and reproducibility.

## Goals / Non-Goals
- **Goals**
  - Deterministic pack build (same workspace state + same inputs → identical pack bytes).
  - Budgeted: pack build MUST stop deterministically when limits are reached.
  - Schema-valid: every pack MUST validate against the local schema pack (`gmsd:aos:schema:context-pack:v1`).
  - Mode-specific: task-mode and phase-mode define allowed artifact sets and stable inclusion order.
- **Non-Goals**
  - Deduplicating content across packs (future optimization).
  - Token-accurate budgeting tied to a specific LLM provider tokenizer (future milestone).
  - Packing arbitrary repository files outside `.aos/**` (explicitly out of scope for now).

## Decisions
### Decision: Add `PCK-####` as a routed artifact ID
Extend routing so packs can be addressed deterministically:
- **ID**: `PCK-####` (4 digits).
- **Contract path**: `.aos/context/packs/PCK-####.json`.

This keeps pack references stable and makes pack discovery simple (by ID + canonical path).

### Decision: Packs are self-contained
Packs embed the selected artifact contents, not just references, so downstream consumers can operate using the pack alone.

Each entry includes:
- `contractPath` (canonical `.aos/*` contract path)
- `contentType` (e.g., `application/json`, `text/plain`)
- `content` (string; for JSON it MUST be canonicalized)
- `bytes` and `sha256` (for integrity + budget accounting)

### Decision: Deterministic budgeting and selection order
Packing is deterministic by defining:
- stable traversal order of candidate artifacts (ordinal ordering over contract paths)
- stable inclusion policy (include-until-budget-exhausted, then stop)
- stable truncation policy (no partial files; entries are either included fully or excluded)

### Decision: CLI surface
Add minimal CLI entrypoints:
- `aos pack build --task <TSK-######>`
- `aos pack build --phase <PH-####>`

Both commands:
- validate the workspace first
- acquire the workspace lock before writing
- write a new pack file to `.aos/context/packs/`
- print the created pack id (`PCK-####`)

## Open questions (deferred)
- Whether `PCK-####` should be included in the public `ArtifactKinds` list (it is not a roadmap item kind today).
- Whether budgets should include a second “token estimate” field once LLM adapters exist.

