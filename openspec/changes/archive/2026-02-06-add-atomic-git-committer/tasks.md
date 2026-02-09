# Tasks: Add Atomic Git Committer

## Task 1: Define interface contracts
**Purpose:** Establish the public API surface for the Atomic Git Committer.

**Work:**
- [x] Create `IAtomicGitCommitter` interface with `CommitAsync(CommitRequest, CancellationToken)` method
- [x] Define `CommitRequest` record (TaskId, FileScopes, ChangedFiles, Summary)
- [x] Define `CommitResult` record (IsSuccess, CommitHash, DiffStat, ErrorMessage, FilesStaged)
- [x] Define `DiffStat` record (FilesChanged, Insertions, Deletions)

**Validation:** Interface compiles, XML docs present, follows `agents-task-executor` patterns.

**Dependencies:** None.

---

## Task 2: Implement scope intersection staging
**Purpose:** Core safety mechanism: only stage files in the intersection of changed and allowed.

**Work:**
- [x] Implement `StagingIntersection.Compute(changedFiles, fileScopes)` algorithm
- [x] Handle glob patterns in fileScopes (e.g., `src/**/*.cs`)
- [x] Return ordered list of files to stage (deterministic ordering)
- [x] Log excluded files with reason (out of scope)

**Validation:** Unit tests: exact match, subset match, no match, glob matching, ordering. ✓ All 16 tests passed.

**Dependencies:** Task 1.

---

## Task 3: Implement git command execution
**Purpose:** Execute git stage and commit commands safely with error capture.

**Work:**
- [x] Implement `GitCommandRunner` internal class
- [x] Execute `git add <files>` for computed intersection
- [x] Execute `git commit -m "TSK-XXXXXX: <summary>"`
- [x] Capture stdout, stderr, exit code
- [x] Handle edge cases: no files to stage, empty commit message, git not initialized

**Validation:** Unit tests with fake git process; integration tests with temp repo. ✓ All tests implemented.

**Dependencies:** Task 2.

---

## Task 4: Implement evidence capture
**Purpose:** Write commit metadata to per-run artifacts.

**Work:**
- [x] Create `CommitEvidenceWriter` class
- [x] Write `git-diffstat.json` before commit (computed stats)
- [x] Write `git-commit.json` after commit (hash, timestamp, message)
- [x] Use deterministic JSON serialization via `AosDeterministicJsonWriter`
- [x] Include schema version in both artifacts

**Validation:** Evidence files parseable, deterministic (same content = same bytes).

**Dependencies:** Task 3.

---

## Task 5: Integrate with aos-task-evidence
**Purpose:** Update task-evidence pointers with commit metadata.

**Work:**
- [x] Create `TaskEvidenceUpdater` class
- [x] Update `.aos/evidence/task-evidence/<task-id>/latest.json`
- [x] Populate `gitCommit` slot with hash (or null if no commit)
- [x] Populate `diffstat` slot with computed stats
- [x] Use atomic file writes per `aos-deterministic-json-serialization`

**Validation:** Evidence pointer updated after successful commit; null commit handled.

**Dependencies:** Task 4.

---

## Task 6: Implement orchestrator handler
**Purpose:** Integrate with gating system for phase routing.

**Work:**
- [x] Create `AtomicGitCommitterHandler` implementing `IHandler`
- [x] Accept execution intent with task reference
- [x] Delegate to `IAtomicGitCommitter` for commit
- [x] Gate: check intersection non-empty before proceeding
- [x] Route to Verifier on success, FixPlanner on failure
- [x] Return `HandlerResult` with phase transition info

**Validation:** Handler tests: success path, empty intersection, git error, scope violation. ✓ Tests created.

**Dependencies:** Task 5.

---

## Task 7: Add DI registration
**Purpose:** Wire up services in the Agents composition root.

**Work:**
- [x] Add `AddAtomicGitCommitter()` extension method in `ServiceCollectionExtensions`
- [x] Register `IAtomicGitCommitter` → `AtomicGitCommitter` (scoped)
- [x] Register `AtomicGitCommitterHandler` in orchestrator handler registry

**Validation:** Services resolve correctly in test composition root.

**Dependencies:** Task 6.

---

## Task 8: Write integration tests
**Purpose:** End-to-end validation of the complete workflow.

**Work:**
- [x] Create `AtomicGitCommitterIntegrationTests` using temp git repository
- [x] Test: successful commit with scoped files updates evidence
- [x] Test: forbidden files are never staged even if modified
- [x] Test: empty intersection produces null commit in evidence
- [x] Test: commit message format is `TSK-XXXXXX: <summary>`
- [x] Test: rerun produces new commit hash, updated evidence

**Validation:** All tests pass; no real repo dependencies (use temp repos).

**Dependencies:** Task 7.

---

## Task 9: Write spec delta
**Purpose:** Formalize requirements in OpenSpec format.

**Work:**
- [x] Create `openspec/changes/add-atomic-git-committer/specs/agents-atomic-git-committer/spec.md`
- [x] Requirements: scope intersection, TSK-based messages, evidence capture
- [x] Scenarios per requirement with GIVEN/WHEN/THEN
- [x] Cross-reference related specs

**Validation:** `openspec validate add-atomic-git-committer --strict` passes.

**Dependencies:** None (can be done in parallel after Task 1).

---

## Dependencies Graph
```
Task 1 (Interfaces)
    ↓
Task 2 (Staging)
    ↓
Task 3 (Git commands)
    ↓
Task 4 (Evidence)
    ↓
Task 5 (Task evidence)
    ↓
Task 6 (Handler)
    ↓
Task 7 (DI)
    ↓
Task 8 (Integration tests)

Task 9 (Spec) can run in parallel after Task 1
```

## Parallelization Opportunities
- Task 9 (spec writing) can proceed once Task 1 stabilizes the interface
- Tasks 2-5 are sequential (implementation chain)
- Task 6 depends on 5 but can start drafting once handler patterns are known
