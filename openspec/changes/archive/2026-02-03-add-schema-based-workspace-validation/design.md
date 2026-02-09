## Context
The AOS workspace (`.aos/*`) is a filesystem contract with separated truth layers (spec/state/evidence/codebase/context/cache). Engine workflows depend on this contract being structurally and semantically valid; correctness must be checked programmatically, not assumed.

The engine already ships an embedded schema pack (`Gmsd.Aos/Resources/Schemas/**`) and seeds a local schema pack under `.aos/schemas/**` during `aos init`. However, workspace validation largely stops at “valid JSON” checks; it does not validate artifacts against JSON Schema.

This change upgrades workspace validation to be schema-based and adds deterministic cross-file invariants for roadmap references.

## Goals / Non-Goals
### Goals
- Validate known workspace artifacts structurally against schemas via `aos validate workspace`.
- Treat JSON Schema `$id` as the canonical schema identifier and load schemas deterministically by `$id`.
- Expand the shipped schema pack to include baseline artifacts (and schema-light placeholders for upcoming artifacts).
- Enforce deterministic cross-file invariants for roadmap item references.
- Produce normalized, machine-readable validation issues (contract path + schema id + instance location + message).

### Non-Goals
- Full semantic validation of milestone/phase/task/uat/issue content beyond schema-light v1 shapes.
- New workflow features beyond validation (planning/execution milestones).
- Arbitrary historical schema migrations beyond deterministic, versioned behavior.

## Decisions
### Decision: Use JSON Schema `$id` as canonical schema identity
We will treat `$id` as the canonical schema identity for both embedded and local schemas. This enables:
- stable schema addressing without relying on filenames
- consistent validation selection across machines
- a public catalog of schema IDs for tooling

### Decision: Use a dedicated JSON Schema validator library
Adopt `JsonSchema.Net` to validate JSON instances (parsed via `System.Text.Json`) against draft 2020-12 schemas.

Rationale:
- aligns with existing schema draft declaration in shipped schemas (`$schema` = draft 2020-12)
- minimizes custom validation logic beyond cross-file invariants

### Decision: Keep local schema registry format stable
Do not change `.aos/schemas/registry.json` schema in this milestone. The registry remains an inventory of schema filenames; `$id` is read from each schema file at load time.

### Decision: Roadmap invariant validation model
Interpret `roadmap.items[]` as artifact references:
- `kind` is a stable kind label
- `id` is an artifact id

Validation enforces:
- `id` parses and matches the kind’s id format
- referenced artifact exists at the canonical contract path
- if a catalog index exists for the kind, the id is present in the index

## Alternatives considered
- **Use filename-base schema IDs** (e.g., `project` from `project.schema.json`): rejected because `$id` is the canonical schema identity in JSON Schema and supports a stable public catalog without coupling to filenames.
- **Make local registry map `$id` → filename**: rejected for now to avoid a breaking registry schema change; we can introduce a v2 registry shape later if needed.
- **Continue “parse-only” workspace validation**: rejected because it cannot catch structural violations and cannot produce stable, machine-readable error reporting.

## Risks / Trade-offs
- Adding a JSON Schema library dependency introduces maintenance cost; mitigate by keeping integration thin and using normalized reporting.
- Shipped schemas for “future” artifacts may evolve; mitigate by keeping those schemas schema-light and versioned.

## Migration plan
No migration is required for the local schema registry format. The change is additive (more schemas + schema-based validation behavior).

## Open questions
- Whether to expose a `--json` output flag for `aos validate workspace` to print only machine-readable envelopes (in addition to the current human output).

