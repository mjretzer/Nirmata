## Context
The AOS workspace (`.aos/*`) is a filesystem contract with separated truth layers (spec/state/evidence/codebase/context/cache). Workflows depend on the contract being structurally and semantically valid; correctness must be checked programmatically, not assumed.

The existing milestone `aos-workspace-bootstrap` establishes deterministic initialization (`aos init`) plus a placeholder `.aos/schemas/registry.json`. This milestone adds the next gate: schema registry + validation commands + invariants.

## Goals / Non-Goals
### Goals
- Provide a schema registry that loads JSON Schemas shipped with the engine so validation is consistent and versioned with `nirmata.Aos`.
- Provide `aos validate schemas` to validate the shipped schema pack (including canonical naming).
- Provide `aos validate workspace` to validate selected layers (default: all) and enforce cross-file invariants.
- Fail fast on malformed JSON and invariant breaches.
- Keep all implementation engine-owned (`nirmata.Aos`) with no Product Application dependencies.

### Non-Goals
- Planning/execution/run-lifecycle features beyond validation (separate roadmap items).
- Full coverage of every future artifact type; this milestone focuses on the artifacts already present (or introduced) at this stage.
- Schema migrations for arbitrary historical workspaces; a one-time rename helper is optional and only applies if on-disk schemas are introduced later.

## Decisions
### Decision: Shipped schemas are embedded resources owned by `nirmata.Aos`
The authoritative schema set is shipped with the engine (embedded JSON files) to ensure:
- deterministic availability (no network)
- versioning with code
- consistent validation behavior across machines

### Decision: Canonical schema filename rules
Schema asset filenames MUST be deterministic and canonical:
- lower-kebab-case base name (e.g., `context-pack`)
- suffix `.schema.json`
- no additional `.` characters besides the `.schema.json` suffix

Example:
- Accept: `context-pack.schema.json`
- Reject: `context.pack.schema.json`

### Decision: Workspace validation is layer-scoped
`aos validate workspace` validates a selected subset of layers (default all) so workflows can gate by scope without re-validating everything when not needed.

### Decision: Invariants are validated separately from JSON Schema
Cross-file integrity rules (e.g., “single-project only”, roadmap referencing constraints) are enforced by an invariant validator in addition to schema checks.

## Alternatives considered
- Store schema files in `.aos/schemas/` and treat them as workspace-owned truth: rejected for now because it complicates upgrades and risks drift. Embedded schemas keep the engine authoritative.
- Omit canonical filename enforcement: rejected because schema naming must be deterministic for tooling, registry lookup, and future migration logic.

## Risks / Trade-offs
- Adding a schema library dependency introduces long-term maintenance cost → mitigate by choosing a stable, widely used .NET JSON Schema validator and keeping integration thin.
- Roadmap schema/reference rules may evolve → mitigate by keeping invariants narrowly scoped to “single project only” and making rules explicit in spec requirements.

## Migration plan (optional)
If/when on-disk schema files are introduced for developer inspection/export, add a one-time migration helper that:
- detects known non-canonical filenames
- renames them deterministically to canonical names
- refuses to overwrite existing canonical files

## Open questions
- Whether to add a `--json` flag to validation commands for machine-readable reports in addition to human output.
- Exact roadmap schema fields used to express “project reference” (to ensure the invariant check is precise and future-proof).

