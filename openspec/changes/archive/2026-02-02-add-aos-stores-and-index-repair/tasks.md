## 1. Implementation
- [x] 1.1 Add store abstractions in `Gmsd.Aos` for spec/state/evidence (read/write/delete + canonical routing + deterministic JSON writes)
- [x] 1.2 Implement spec-store support for `project.json`, `roadmap.json`, and catalog indexes (milestones/phases/tasks/issues/uat)
- [x] 1.3 Implement state-store baseline artifacts:
  - [x] create/read/write `.aos/state/state.json`
  - [x] append/read `.aos/state/events.ndjson` (append-only)
- [x] 1.4 Implement evidence-store baseline artifacts:
  - [x] write/update `.aos/evidence/logs/commands.json`
  - [x] write `.aos/evidence/runs/<run-id>/manifest.json` for produced outputs (paths + hashes)
- [x] 1.5 Add CLI command `aos repair indexes`:
  - [x] rebuild spec catalog indexes deterministically from disk state
  - [x] rebuild run index deterministically from run metadata on disk
  - [x] fail with actionable errors when repair is not possible
- [x] 1.6 Extend `aos init` to seed required baseline artifacts (new indexes + state/evidence stubs) and update compliance checks accordingly
- [x] 1.7 Extend `aos validate workspace` to validate new required artifacts with clear diagnostics and pointers to repair

## 2. Tests & Validation
- [x] 2.1 Add/extend deterministic fixture tests for `aos init` to include the new seeded artifacts
- [x] 2.2 Add tests for `aos repair indexes` determinism (same workspace state → same bytes)
- [x] 2.3 Add workspace validation tests for malformed/absent:
  - [x] spec indexes (including issues/uat)
  - [x] `state.json` and `events.ndjson`
  - [x] evidence command logs and run manifests
- [x] 2.4 Run `openspec validate add-aos-stores-and-index-repair --strict` and fix any spec formatting issues

