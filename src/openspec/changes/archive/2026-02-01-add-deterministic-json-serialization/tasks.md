## 1. Implementation
- [x] 1.1 Add a canonical deterministic JSON writer utility in `nirmata.Aos` that:
  - canonicalizes JSON objects by recursively sorting keys using ordinal string ordering
  - writes UTF-8 (no BOM) with LF line endings and a trailing newline
  - writes atomically (temp + replace/move) and best-effort cleans up temp files
  - avoids churn by not rewriting when canonical bytes are identical
- [x] 1.2 Update `nirmata.Aos/Engine/Workspace/AosWorkspaceBootstrapper.cs` to write all init-created JSON artifacts using the canonical writer.
- [x] 1.3 Update `nirmata.Aos/Engine/Evidence/Runs/AosRunEvidenceScaffolder.cs` to write `run.json` and `.aos/evidence/runs/index.json` using the canonical writer (including overwrite/no-churn behavior).
- [x] 1.4 Update `nirmata.Aos/Engine/Evidence/ExecutePlan/ExecutePlanActionsLogWriter.cs` to write `execute-plan.actions.json` using the canonical writer.

## 2. Tests
- [x] 2.1 Add unit tests that prove canonical recursive key ordering (nested objects; dictionary-backed inputs) produces stable bytes.
- [x] 2.2 Add unit tests for the no-churn rule (same canonical bytes does not rewrite).
- [x] 2.3 Add a deterministic, testable atomic-write failure path (e.g., test hook to fail after temp write but before commit) and verify the target file remains valid/unmodified on failure.
- [x] 2.4 Update/extend existing determinism tests (e.g., `AosInitFixtureTests`, `AosExecutePlanDeterminismTests`) as needed to reflect canonical ordering and atomic/no-churn semantics.

## 3. Spec/tooling validation
- [x] 3.1 Run `openspec validate add-deterministic-json-serialization --strict` and fix any validation issues.
- [x] 3.2 Ensure the change satisfies roadmap exit criteria: identical inputs produce identical JSON across runs/hosts for `.aos/**` artifacts.
