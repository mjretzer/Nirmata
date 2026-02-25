## Context
The engine (“AOS”) relies on a canonical workspace rooted at `.aos/` to separate truth layers (spec/state/evidence/context/codebase/cache). The roadmap item `add-aos-workspace-bootstrap` establishes the first enforceable step: creating the workspace skeleton deterministically.

## Goals / Non-Goals
### Goals
- Provide `aos init` that creates a canonical `.aos/*` structure consistently across machines.
- Keep the workspace bootstrap business logic and the `aos init` command handler in `Gmsd.Aos` (engine-owned), with no Product Application dependencies.
- Ensure created files are deterministic (no host paths, usernames, machine IDs, timestamps).
- Seed minimal authoring entrypoints:
  - `.aos/spec/project.json`
  - baseline index files for milestones/phases/tasks
- Provide minimal schema registry “wiring” placeholders sufficient to support the next milestone (`add-schema-registry-and-validation-gate`).
- Make the command safe to re-run (idempotent).

### Non-Goals
- Full schema validation (`aos validate ...`) or invariant enforcement (next roadmap item).
- Run lifecycle scaffolding under `.aos/evidence/runs/**` (later roadmap item).
- Planning/execution workflows (later roadmap items).
- Any Product Application changes (this is engine-only).

## Decisions
### Decision: `aos init` is deterministic + idempotent
- If `.aos/` does not exist: create the full skeleton.
- If `.aos/` exists and is compliant: succeed without modifying content (no churn).
- If `.aos/` exists but is non-compliant: fail with an actionable error that lists missing/extra/invalid paths.

### Decision: Avoid machine-specific values in generated JSON
Generated JSON documents MUST not embed:
- absolute paths
- usernames / machine names
- timestamps

This keeps golden fixtures stable and supports cross-machine reproducibility.

### Decision: Minimal schema registry placeholders live in `.aos/schemas/`
`aos init` creates `.aos/schemas/` and writes placeholder documents that establish conventions and make the upcoming registry/validator work straightforward, without committing to a full schema set in this milestone.

## Alternatives Considered
- Only create folders (no files): rejected because the roadmap explicitly requires seeding `.aos/spec/project.json` and baseline indexes.
- Allow timestamps (e.g., `createdAt`): rejected because it breaks deterministic fixture comparison without introducing a normalization layer.
- “Merge” behavior when `.aos/` exists: rejected for now; failing on non-compliance is safer until invariants/migrations exist.

## Risks / Trade-offs
- A strict “fail on non-compliance” may be annoying during development → mitigated by clear diagnostics and idempotent success when compliant.
- Placeholder schema registry artifacts might need renaming later → mitigated by keeping them minimal and aligning naming with the roadmap’s canonical conventions.

## Migration Plan
- None for this milestone (new workspace).
- If `.aos/` already exists in a repository, `aos init` will either:
  - detect compliance and no-op, or
  - fail and require manual cleanup until dedicated migrations are introduced.

## Open Questions
- Exact packaging of the CLI as `aos` (local dotnet tool vs app in solution) for developer ergonomics, while keeping command logic owned by `Gmsd.Aos`.
- Which placeholder schema artifact names we standardize in this milestone vs the next (registry vs conventions).

