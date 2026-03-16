# Spec: E2E Verification Tests

**Capability:** e2e-verification

---

## ADDED Requirements

### Requirement: E2E-VRF-001 — Init workspace verification

The E2E tests MUST verify that `aos init` creates a valid workspace with all six required AOS layers and a valid project spec.

#### Scenario: Init creates all six AOS layers

- **Given** a clean fixture repository
- **When** `aos init` is executed
- **Then** the following directories exist:
  - `.aos/schemas/`
  - `.aos/spec/`
  - `.aos/state/`
  - `.aos/evidence/`
  - `.aos/context/`
  - `.aos/codebase/`
  - `.aos/cache/`

#### Scenario: Init creates valid project spec

- **Given** a repository where init has run
- **When** `.aos/spec/project.json` is read
- **Then** it contains valid JSON with required fields
- **And** it passes schema validation

### Requirement: E2E-VRF-002 — Init idempotency

The E2E tests MUST verify that `aos init` is idempotent and safe to run multiple times without destructive side effects.

#### Scenario: Init is safe to run multiple times

- **Given** a repository where `aos init` has already run
- **When** `aos init` is executed again
- **Then** no existing files are destructively overwritten
- **And** exit code is 0 (success)

#### Scenario: Init preserves existing state

- **Given** a repository with existing `.aos/state/cursor.json`
- **When** `aos init` is run again
- **Then** the cursor.json content is preserved
- **And** state continuity is maintained

### Requirement: E2E-VRF-003 — Validation gates after init

The E2E tests MUST verify that all validation gates pass on a freshly initialized workspace.

#### Scenario: Schema validation succeeds after init

- **Given** a freshly initialized repository
- **When** `aos validate schemas` is executed
- **Then** the command succeeds with exit code 0
- **And** all schema files are validated

#### Scenario: State validation succeeds after init

- **Given** a freshly initialized repository
- **When** `aos validate state` is executed
- **Then** the command succeeds with exit code 0
- **And** state integrity checks pass

#### Scenario: Evidence validation succeeds after init

- **Given** a freshly initialized repository
- **When** `aos validate evidence` is executed
- **Then** the command succeeds with exit code 0
- **And** evidence directory structure is valid

### Requirement: E2E-VRF-004 — Full control loop execution

The E2E tests MUST verify the complete agent control loop from bootstrap through execution, verification, and fix phases.

#### Scenario: Bootstrap phase creates valid spec

- **Given** a clean repository
- **When** `aos init` → seed project → roadmap generate → validate
- **Then** the spec is valid and cursor points to first milestone/phase

#### Scenario: Planning phase creates tasks

- **Given** a bootstrapped repository
- **When** a phase is created with 2 atomic tasks
- **Then** `task.json` and `plan.json` are persisted
- **And** task IDs follow `TSK-*` pattern
- **And** scope is explicitly defined

#### Scenario: Execution phase runs through orchestrator

- **Given** a planned phase
- **When** `execute-plan` runs via Orchestrator
- **Then** fresh subagent is created per step
- **And** only allowed files are touched (scope enforcement)
- **And** evidence is written to `.aos/evidence/runs/RUN-*`

#### Scenario: Verification phase produces controlled failure

- **Given** executed tasks with intentional defect
- **When** `verify-work` runs via UAT Verifier
- **Then** verification fails
- **And** `ISS-*.json` is created with issue details
- **And** UAT artifacts are persisted

#### Scenario: Fix phase resolves and advances

- **Given** a failed verification with issue file
- **When** Fix Planner generates fix tasks
- **And** fix tasks are executed
- **And** `verify-work` is re-run
- **Then** verification passes
- **And** cursor advances to verified-pass state

### Requirement: E2E-VRF-005 — Test categorization

E2E tests MUST be categorized with the `[Trait("Category","E2E")]` attribute to enable selective inclusion/exclusion in test runs.

#### Scenario: E2E tests are categorized

- **Given** E2E test implementations
- **When** tests are written
- **Then** they include `[Trait("Category","E2E")]` attribute
- **And** fast tests exclude E2E category by default
