## Context
`Gmsd.Aos` is the engine library that hosts workspace contracts, schemas/validation, deterministic IO, and orchestration primitives. Multiple projects (agents and hosts, and potentially product-facing surfaces later) need to *compile against* engine capabilities.

Without a clear boundary, callers can:
- reference internal namespaces/classes directly (creating coupling), and/or
- consume public APIs that accidentally expose internal types (making internal refactors breaking).

This change defines a stable public API boundary and introduces enforcement to keep internals swappable.

## Goals / Non-Goals
### Goals
- Make `Gmsd.Aos.Public.*` the **only supported compile-against surface** for consumers.
- Keep `Gmsd.Aos.Engine.*` and `Gmsd.Aos._Shared.*` **internal-only** (implementation details).
- Provide a predictable place for stable contract types (`Gmsd.Aos.Contracts.*`) used by the public surface (IDs, contract shapes).
- Enforce the boundary in CI/build so violations fail fast.

### Non-Goals
- Splitting the engine into multiple assemblies/packages in this phase.
- Introducing runtime behavior changes or `.aos/*` workspace outputs.
- Completing all method-level API design for every service; focus is the *shape and enforcement of the boundary*.

## Decisions
### Decision: Single-assembly boundary using folders + namespaces
We will keep one `Gmsd.Aos` assembly and establish a strong convention:
- Public surface: `Gmsd.Aos/Public/**` → `Gmsd.Aos.Public.*`
- Contracts: `Gmsd.Aos/Contracts/**` → `Gmsd.Aos.Contracts.*`
- Internals: `Gmsd.Aos/Engine/**` → `Gmsd.Aos.Engine.*` (internal implementation)
- Internals: `Gmsd.Aos/_Shared/**` → `Gmsd.Aos._Shared.*` (internal shared helpers)

This is the smallest step that creates a compile-against contract without introducing packaging complexity.

### Decision: Public surface consists of interfaces + catalogs only (initially)
The initial public surface is:
- service interfaces (`IWorkspace`, `ISpecStore`, `IStateStore`, `IEvidenceStore`, `IValidator`, `ICommandRouter`)
- catalogs/constants for stable IDs and kinds (`SchemaIds`, `CommandIds`, `ArtifactKinds`)

Concrete implementations live under internal namespaces and are returned/constructed through public entrypoints (to be defined in follow-up milestones).

### Decision: Enforce boundary with a build-time quality gate
The enforcement must detect:
- **No public leakage**: no public types in `Gmsd.Aos.Engine.*` / `Gmsd.Aos._Shared.*`, and no public members (including generic type arguments) that reference internal types.
- **No external compile-against**: consumer projects should not be able to reference internal namespaces/types.

Implementation options (ranked):
1) A unit test (reflection-based) that scans the built `Gmsd.Aos` assembly for forbidden public API surface.
2) A Roslyn analyzer/package for compile-time diagnostics (more robust but higher effort).
3) MSBuild target heuristics (fast but brittle).

We start with the simplest deterministic, actionable gate (1) and evolve to (2) if needed.

## Risks / Trade-offs
- **False confidence if enforcement is shallow** → mitigate by scanning full public API surface including nested/generic member types.
- **Slower evolution if the public surface is too broad** → mitigate by keeping the public surface minimal (interfaces + catalogs only) and preferring additive changes.
- **Tests need access to internals** → mitigate by testing via reflection without `InternalsVisibleTo` whenever possible.

## Migration Plan
- Introduce the public/contract folder + namespace conventions.
- Add the new public interfaces/catalogs as skeletons (no runtime behavior changes).
- Introduce enforcement tests/gates and ensure CI fails on boundary violations.
- Incrementally migrate any future consumers to compile against `Gmsd.Aos.Public.*` only.

## Open Questions
- Should `Gmsd.Aos.Contracts.*` be limited to ID catalogs + wire contracts only, or also include shared primitives used by both public and internal code?
- Do we want a single public entrypoint (e.g., `AosEngine.Create(...)`) in this phase, or leave instantiation patterns to later milestones?

