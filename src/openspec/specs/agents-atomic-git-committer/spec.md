# agents-atomic-git-committer Specification

## Purpose
Deterministic, scope-aware Git committer for task execution workflows. Ensures task-scoped files are committed atomically with traceable TSK-based messages and captures evidence for auditability.

## Requirements

### Requirement: Atomic Git Committer interface exists
The system SHALL provide an `IAtomicGitCommitter` interface in `nirmata.Agents.Execution.Execution.AtomicGitCommitter` that executes atomic Git commits scoped to task-defined file patterns.

The interface MUST define:
- `CommitAsync(CommitRequest request, CancellationToken ct)` → returns `Task<CommitResult>`

`CommitRequest` MUST include:
- `TaskId` (string): Canonical task ID in format `TSK-######`
- `FileScopes` (array): File path patterns allowed for modification (e.g., `["src/**/*.cs", "tests/**/*.cs"]`)
- `ChangedFiles` (array): List of file paths that have been changed and are candidates for staging
- `Summary` (string): Summary message to include in the commit (prefixed with task ID)

`CommitResult` MUST include:
- `IsSuccess` (bool): Whether the commit operation succeeded
- `CommitHash` (string|null): The Git commit hash if successful; otherwise null
- `DiffStat` (object|null): Statistics about the diff (files changed, insertions, deletions)
- `ErrorMessage` (string|null): Error details if the commit failed
- `FilesStaged` (array): List of files that were actually staged and committed

`DiffStat` MUST include:
- `FilesChanged` (int): Number of files changed
- `Insertions` (int): Number of lines inserted
- `Deletions` (int): Number of lines deleted

#### Scenario: Commit succeeds with scoped files
- **GIVEN** a task with ID `TSK-0001`, file scopes `["src/services/*.cs"]`, and changed files `["src/services/AuthService.cs", "tests/AuthTests.cs"]`
- **WHEN** `CommitAsync` is called with summary `"Add authentication"`
- **THEN** only `src/services/AuthService.cs` is staged, a commit is created with message `"TSK-0001: Add authentication"`, and the result contains a valid commit hash with diff statistics

#### Scenario: Empty intersection produces null commit
- **GIVEN** a task with file scopes `["src/models/*.cs"]` and changed files `["src/services/AuthService.cs"]`
- **WHEN** `CommitAsync` is called
- **THEN** no commit is made, `CommitResult.IsSuccess` is `true`, `CommitHash` is `null`, and `FilesStaged` is empty

---

### Requirement: Scope intersection staging
The system SHALL compute the intersection of changed files and allowed scopes before any Git operations, ensuring only files in scope are staged.

The intersection algorithm MUST:
- Support glob patterns in `fileScopes` (e.g., `src/**/*.cs`, `*.md`)
- Return an ordered list of files (deterministic ordering for reproducibility)
- Exclude files outside scope with logging
- Handle exact paths and wildcard patterns

#### Scenario: Exact path matches scope
- **GIVEN** file scopes `["src/models/User.cs"]` and changed files `["src/models/User.cs"]`
- **WHEN** `StagingIntersection.Compute` is called
- **THEN** the intersection contains `["src/models/User.cs"]`

#### Scenario: Glob pattern matches multiple files
- **GIVEN** file scopes `["src/**/*.cs"]` and changed files `["src/models/User.cs", "src/services/Auth.cs", "tests/UserTests.cs"]`
- **WHEN** `StagingIntersection.Compute` is called
- **THEN** the intersection contains `["src/models/User.cs", "src/services/Auth.cs"]` (sorted alphabetically)

#### Scenario: No intersection results in empty staging
- **GIVEN** file scopes `["src/models/*.cs"]` and changed files `["docs/README.md", ".gitignore"]`
- **WHEN** `StagingIntersection.Compute` is called
- **THEN** the intersection is empty and no files are staged

---

### Requirement: Git command execution with error capture
The system SHALL execute Git commands safely with stdout, stderr, and exit code capture.

`GitCommandRunner` MUST:
- Execute `git add <files>` for the computed intersection
- Execute `git commit -m "TSK-XXXXXX: <summary>"` with the TSK-based message format
- Capture and return stdout, stderr, and exit code
- Handle edge cases: no files to stage, empty commit message, Git not initialized

#### Scenario: Git add succeeds for valid files
- **GIVEN** an intersection of `["src/models/User.cs"]` in a valid Git repository
- **WHEN** `git add` is executed
- **THEN** the file is staged and the command returns success (exit code 0)

#### Scenario: Git commit with TSK-based message
- **GIVEN** a successful `git add` and task ID `TSK-0001` with summary `"Update user model"`
- **WHEN** `git commit` is executed
- **THEN** the commit is created with message `"TSK-0001: Update user model"` and the commit hash is captured

#### Scenario: Git error is captured
- **GIVEN** a Git repository with merge conflicts or locks
- **WHEN** `git commit` is executed
- **THEN** the error is captured in `stderr`, a non-zero exit code is returned, and `CommitResult.ErrorMessage` contains the Git error details

---

### Requirement: Evidence capture per run
The system SHALL write commit metadata to per-run artifacts for auditability.

`CommitEvidenceWriter` MUST:
- Write `git-diffstat.json` before commit with computed statistics
- Write `git-commit.json` after commit with hash, timestamp, and message
- Use deterministic JSON serialization per `aos-deterministic-json-serialization`
- Include schema version in both artifacts
- Write to `.aos/evidence/runs/<run-id>/artifacts/`

`git-diffstat.json` MUST contain:
- `schemaVersion` (integer)
- `filesChanged` (integer)
- `insertions` (integer)
- `deletions` (integer)

`git-commit.json` MUST contain:
- `schemaVersion` (integer)
- `commitHash` (string|null)
- `commitMessage` (string)
- `timestamp` (ISO 8601 string)
- `filesStaged` (array)

#### Scenario: Evidence written after successful commit
- **GIVEN** a successful commit with hash `abc123`, message `"TSK-0001: Fix bug"`, and diffstat (2 files, 10 insertions, 3 deletions)
- **WHEN** evidence is captured
- **THEN** `git-commit.json` contains the hash and message, and `git-diffstat.json` contains the statistics

#### Scenario: Evidence written for empty intersection
- **GIVEN** an empty intersection with no files to commit
- **WHEN** evidence is captured
- **THEN** `git-commit.json` contains `commitHash: null`, `git-diffstat.json` contains zeros for all statistics

---

### Requirement: Task evidence pointer updates
The system SHALL update task-evidence pointers with commit metadata.

`TaskEvidenceUpdater` MUST:
- Update `.aos/evidence/task-evidence/<task-id>/latest.json`
- Populate `gitCommit` slot with hash (or null if no commit)
- Populate `diffstat` slot with computed stats
- Use atomic file writes per `aos-deterministic-json-serialization`
- Support both commit and no-commit scenarios

#### Scenario: Task evidence updated with commit
- **GIVEN** a successful commit with hash `abc123` for task `TSK-0001`
- **WHEN** `UpdateWithCommit` is called
- **THEN** `.aos/evidence/task-evidence/TSK-0001/latest.json` contains `gitCommit: "abc123"` and the diffstat

#### Scenario: Task evidence updated without commit
- **GIVEN** an empty intersection for task `TSK-0001`
- **WHEN** `UpdateWithoutCommit` is called
- **THEN** `.aos/evidence/task-evidence/TSK-0001/latest.json` contains `gitCommit: null` and zeroed diffstat

---

### Requirement: Handler integration with orchestrator
The system SHALL provide an `AtomicGitCommitterHandler` that integrates with the orchestrator's gating and phase routing system.

The handler MUST:
- Accept execution intent with task reference via `CommandRequest`
- Extract file scopes from task plan at `.aos/spec/tasks/<task-id>/plan.json`
- Gate: check intersection non-empty before proceeding
- Delegate to `IAtomicGitCommitter` for commit execution
- Route to success path (Verifier) on success, failure path (FixPlanner) on failure
- Return `CommandRouteResult` with phase transition info

#### Scenario: Handler executes commit and routes to Verifier
- **GIVEN** a task with valid file scopes and modified files within scope
- **WHEN** the handler is invoked by the orchestrator
- **THEN** it executes the atomic commit and returns success, routing to Verifier phase

#### Scenario: Handler routes to FixPlanner on empty intersection
- **GIVEN** a task with file scopes that do not intersect with changed files
- **WHEN** the handler is invoked
- **THEN** it records null commit evidence and returns failure, routing to FixPlanner phase

#### Scenario: Handler routes to FixPlanner on Git error
- **GIVEN** a Git repository with a lock file or other error condition
- **WHEN** the handler is invoked and Git operations fail
- **THEN** it captures the error, records evidence, and routes to FixPlanner phase

---

### Requirement: DI registration
The system SHALL wire up Atomic Git Committer services in the Agents composition root.

`ServiceCollectionExtensions` MUST:
- Provide `AddAtomicGitCommitter()` extension method
- Register `IAtomicGitCommitter` → `AtomicGitCommitter` with appropriate lifetime
- Register `AtomicGitCommitterHandler` in the handler registry

#### Scenario: Services resolve correctly
- **GIVEN** a configured service collection with `AddAtomicGitCommitter()` called
- **WHEN** `IAtomicGitCommitter` or `AtomicGitCommitterHandler` is resolved
- **THEN** the services are instantiated with their dependencies

---

## Safety Mechanisms

### Forbidden File Protection
Files outside the allowed scope are NEVER staged, even if explicitly requested. Violations are logged but do not fail the commit (other files proceed).

### Empty Intersection Handling
If no changed files match the scope, no commit is made. Evidence records `gitCommit: null` and the handler routes to FixPlanner with a descriptive error.

### Git Error Handling
Git command failures are captured in evidence with exit code and stderr. Partial evidence writes are prevented via atomic JSON writes.

## Determinism Considerations
- Commit timestamps are captured but not considered part of the deterministic outcome
- Diffstat is deterministic based on staged content
- Commit hash may vary if git user config differs (acceptable variance)
- Evidence JSON uses canonical deterministic formatting

## Cross-References
- `aos-task-evidence` — Task evidence pointer format and slots
- `agents-task-executor` — File scope enforcement patterns
- `aos-deterministic-json-serialization` — JSON serialization requirements
- `aos-run-lifecycle` — Per-run artifact directory structure
- `aos-evidence-store` — Evidence storage conventions
