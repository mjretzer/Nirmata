## 1. Implementation
- [x] 1.1 Update routing contract paths for Issue/UAT
  - [x] Update `nirmata.Aos/Engine/Paths/AosPathRouter.cs` Issue/UAT paths to `.aos/spec/issues/<id>.json` and `.aos/spec/uat/<id>.json`
  - [x] Update routing tests (e.g., `tests/nirmata.Aos.Tests/AosPathRouterTests.cs`)

- [x] 1.2 Update SpecStore CRUD surface
  - [x] Add CRUD methods in `nirmata.Aos/Engine/Stores/AosSpecStore.cs` for:
    - milestone (`milestone.json`)
    - phase (`phase.json`)
    - task (`task.json`)
    - issue (`ISS-####.json`)
    - uat (`UAT-####.json`)
  - [x] Add CRUD methods for task sub-artifacts:
    - `.aos/spec/tasks/<id>/plan.json`
    - `.aos/spec/tasks/<id>/links.json`
  - [x] Ensure create/delete operations update catalog indexes deterministically:
    - `.aos/spec/milestones/index.json`
    - `.aos/spec/phases/index.json`
    - `.aos/spec/tasks/index.json`
    - `.aos/spec/issues/index.json`
    - `.aos/spec/uat/index.json`

- [x] 1.3 Update index repair for Issue/UAT discovery
  - [x] Update `nirmata.Aos/Engine/Repair/AosIndexRepairer.cs` to rebuild Issue/UAT indexes from flat `*.json` artifacts under:
    - `.aos/spec/issues/*.json` (excluding `index.json`)
    - `.aos/spec/uat/*.json` (excluding `index.json`)
  - [x] Update repair determinism tests (e.g., `tests/nirmata.Aos.Tests/AosRepairIndexesDeterminismTests.cs`)

- [x] 1.4 Update validation expectations as needed
  - [x] Ensure any contract-path diagnostics that mention Issue/UAT use the new flat paths (routing-based)

- [x] 1.5 Tests for PH-ENG-0004 verification points
  - [x] Add/adjust tests for create/update/show/list flows for each spec artifact type
  - [x] Add/adjust “delete index then rebuild” determinism tests for all catalog indexes

## 2. Spec / Tooling
- [ ] 2.1 Update public surface contracts if needed
  - [ ] If SpecStore is intended to be consumer-facing now, define minimal `nirmata.Aos.Public.ISpecStore` methods and stable contract types under `nirmata.Aos.Contracts` (otherwise keep internal and defer as follow-up)

## 3. Validation
- [x] 3.1 Run `openspec validate update-spec-store-crud-indexing --strict`
- [x] 3.2 Run unit tests relevant to routing/repair/spec store changes

