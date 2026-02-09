## Context
The engine uses `.aos/*` as a deterministic workspace. Spec-layer artifacts under `.aos/spec/**` are the “intended truth” layer, and are validated via schemas and cross-file invariants.

Today, the engine already has:
- deterministic JSON writing (`DeterministicJsonFileWriter`)
- SpecStore primitives for project/roadmap + deterministic catalog index maintenance (`AosSpecStore`)
- deterministic index rebuild (`AosIndexRepairer`)

However:
- SpecStore CRUD for milestone/phase/task/issue/uat artifacts is not yet implemented.
- The current routing contract for Issue/UAT is folder-based, but the desired workspace output shape is flat for Issue/UAT.
- Task sub-artifacts (`plan.json`, `links.json`) are part of the intended workspace output but not part of SpecStore CRUD requirements today.

## Goals / Non-Goals
- **Goals**:
  - Define canonical contract paths and CRUD semantics for spec artifacts required by PH-ENG-0004.
  - Keep all writes deterministic and index updates deterministic.
  - Ensure index repair deterministically rebuilds indexes from on-disk spec artifacts.
  - Make the behavior testable (create/update/show/list; delete index then rebuild is stable).
- **Non-Goals**:
  - Implement StateStore/EvidenceStore (other phases of MS-ENG-0004).
  - Introduce new persistence mechanisms beyond filesystem JSON under `.aos/`.
  - Broaden the public API surface beyond what is required to support SpecStore.

## Decisions
- **Decision: Issue/UAT contract paths are flat files (breaking change)**
  - **New canonical paths**:
    - `.aos/spec/issues/ISS-####.json`
    - `.aos/spec/uat/UAT-####.json`
  - **Rationale**: Matches the roadmap workspace output shape and simplifies discovery for tooling and humans.
  - **Alternatives considered**:
    - Keep folder-based layout for Issue/UAT: rejected due to mismatch with desired output and extra directory churn.
    - Support both layouts indefinitely: rejected due to ambiguity and violating “routing is the only source of truth”.

- **Decision: Task sub-artifacts (`plan.json`, `links.json`) are SpecStore-managed**
  - **Rationale**: Roadmap includes these as first-class spec artifacts under `.aos/spec/tasks/<id>/` and downstream workflows (task-mode, execution) consume them.

- **Decision: Fail-fast on non-canonical layouts; keep repair focused**
  - Routing remains the single source of truth; consumers should not attempt to “guess” alternate file shapes.
  - Index repair rebuilds indexes for the canonical layout and surfaces actionable diagnostics for malformed or unexpected shapes.

## Risks / Trade-offs
- **Breaking change risk**: existing workspaces may contain legacy folder-based Issue/UAT artifacts.
  - **Mitigation**: update specs/tests to make the canonical layout explicit; provide actionable diagnostics and a migration note. If needed, add a future `aos migrate spec` command as a separate milestone.

## Migration Plan
- Update routing + repair + tests to reflect canonical flat Issue/UAT file layout.
- Document how to migrate legacy artifacts:
  - Move `.aos/spec/issues/ISS-0001/issue.json` → `.aos/spec/issues/ISS-0001.json`
  - Move `.aos/spec/uat/UAT-0001/uat.json` → `.aos/spec/uat/UAT-0001.json`
  - Re-run `aos repair indexes` to rebuild indexes deterministically.

