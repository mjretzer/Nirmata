# Change: Add AOS public API surface (“compile-against” contract)

## Why
The AOS engine needs a **stable compile-time contract** so other projects can depend on engine capabilities without coupling to internal implementation details. Today, engine code is organized as a single library and callers can accidentally compile against internal namespaces/types, making the engine harder to evolve or swap.

## What Changes
- Define a stable public surface rooted at `nirmata.Aos/Public/**` (`nirmata.Aos.Public.*` namespaces) as the only supported compile-against contract.
- Introduce `nirmata.Aos/Contracts/**` for stable contract types (wire/artifact contracts and ID catalogs) referenced by the public surface.
- Establish internal-only implementation namespaces rooted at `nirmata.Aos/Engine/**` and `nirmata.Aos/_Shared/**`.
- Add a build-time quality gate that fails when:
  - external projects compile against AOS internals, or
  - AOS public APIs leak internal types/namespaces.

## Non-Goals
- Publishing a separate `nirmata.Aos.Public` NuGet/package or a separate assembly (this change is about the API boundary inside the existing `nirmata.Aos` project).
- Finalizing full v1 interface method shapes for every engine subsystem (this is a skeleton boundary to unblock later milestones).
- Any changes to `.aos/*` workspace artifacts or engine behavior (no new runtime outputs in this phase).

## Impact
- **Affected specs**:
  - new: `aos-public-api-surface`
  - modified: `quality-gates`
- **Affected code** (expected during implementation):
  - `nirmata.Aos/Public/**`, `nirmata.Aos/Contracts/**` (new public surface + contracts)
  - `nirmata.Aos/Engine/**`, `nirmata.Aos/_Shared/**` (internal implementations and shared internals)
  - Build/test enforcement (e.g., analyzer/test) to prevent public leakage

