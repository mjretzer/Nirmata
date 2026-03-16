## 1. Implementation

### 1.1 Core Verifier Interface
- [x] Define `IUatVerifier` interface with `VerifyAsync(UatVerificationRequest, CancellationToken)` method
- [x] Define `UatVerificationRequest` record with `TaskId`, `RunId`, `AcceptanceCriteria[]`, `FileScopes[]`
- [x] Define `UatVerificationResult` record with `IsPassed`, `RunId`, `Checks[]`, `IssuesCreated[]`

### 1.2 UAT Check Implementation
- [x] Implement check runner that evaluates acceptance criteria against execution evidence
- [x] Support check types: `file-exists`, `content-contains`, `build-succeeds`, `test-passes`
- [x] Map each acceptance criterion to a check with clear pass/fail semantics

### 1.3 UAT Artifact Schema & Storage
- [x] Define `UatResult` schema (JSON) with `schemaVersion`, `runId`, `taskId`, `status`, `checks[]`, `timestamp`
- [x] Implement deterministic JSON writer integration for `.aos/evidence/runs/RUN-*/artifacts/uat-results.json`
- [x] Create UAT spec artifact at `.aos/spec/uat/UAT-{taskId}.json` for tracking UAT definitions

### 1.4 Issue Creation on Failure
- [x] Define `Issue` schema with `id`, `scope`, `repro`, `expected`, `actual`, `severity`, `parentUatId`
- [x] Implement `IIssueWriter` for creating `ISS-{n}.json` under `.aos/spec/issues/`
- [x] Map failed checks to structured issues with clear reproduction steps

### 1.5 VerifierHandler Integration
- [x] Create `VerifierHandler` implementing the orchestrator handler pattern
- [x] Integrate with `IUatVerifier` for actual verification work
- [x] Route to FixPlanner on verification failure (return handler result with `NextPhase: "FixPlanner"`)
- [x] Route to completion on verification success

### 1.6 Gating Engine Updates
- [x] Update `GatingEngine.EvaluateAsync` to recognize verification completion state
- [x] Add transition logic: `Verifier` → `FixPlanner` when verification fails
- [x] Add transition logic: `Verifier` → next phase or complete when verification passes

## 2. Validation
- [x] Unit tests: `UatVerifierTests` with mocked acceptance criteria
- [x] Integration tests: End-to-end verification flow with temp workspace
- [x] Schema validation: UAT artifacts pass `aos-workspace-validation`
- [x] Orchestrator routing tests: Verify FixPlanner routing on failure

## 3. Dependencies
- Requires: `agents-task-executor` (provides execution results for verification)
- Requires: `aos-evidence-store` (for reading run artifacts)
- Requires: `aos-deterministic-json-serialization` (for artifact writing)

## 4. Verify Criteria
- [x] Schema-valid UAT artifact produced at `.aos/evidence/runs/RUN-*/artifacts/uat-results.json`
- [x] On failure: `ISS-{n}.json` created under `.aos/spec/issues/` with repro + expected + actual + scope
- [x] On failure: orchestrator routes to FixPlanner phase
- [x] On success: orchestrator continues to next task or completion
- [x] Deterministic JSON serialization for all artifacts
