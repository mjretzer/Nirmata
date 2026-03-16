# agents-atomic-git-committer Specification

## Purpose
The Atomic Git Committer provides deterministic, scope-aware git operations for task execution workflows. It ensures that only files within the task's allowed scope are committed, generates traceable TSK-based commit messages, and captures commit metadata as evidence. This spec bridges task execution (file modifications) with evidence capture (commit hash and diffstat).

## ADDED Requirements

### Requirement: Atomic Git Committer interface exists
The system SHALL provide an `IAtomicGitCommitter` interface in `nirmata.Agents.Execution.Execution.AtomicGitCommitter` that performs scope-intersection staging and deterministic committing.

The interface MUST define:
- `CommitAsync(CommitRequest request, CancellationToken ct)` → returns `Task<CommitResult>`

`CommitRequest` MUST include:
- `TaskId` (string): Canonical task ID in format `TSK-######`
- `FileScopes` (array): Allowed file scope patterns from task plan
- `ChangedFiles` (array): List of files that were modified during execution
- `Summary` (string): Human-readable summary for commit message

`CommitResult` MUST include:
- `IsSuccess` (bool): Whether commit completed successfully
- `CommitHash` (string|null): Git commit hash (null if no commit made)
- `DiffStat` (object): Files changed, insertions, deletions counts
- `ErrorMessage` (string|null): Error details if failed
- `FilesStaged` (array): List of files actually staged and committed

#### Scenario: Committer creates commit for scoped files
- **GIVEN** a task execution that modified `src/Service.cs` and `tests/ServiceTests.cs`
- **AND** the task plan allows scope `src/**/*.cs`
- **WHEN** `CommitAsync` is called with the task ID and file information
- **THEN** only `src/Service.cs` is staged and committed
- **AND** the commit message is `TSK-0001: <summary>`
- **AND** the result contains the commit hash and correct diffstat

#### Scenario: Committer handles empty intersection gracefully
- **GIVEN** a task execution that modified only `tests/ServiceTests.cs`
- **AND** the task plan allows scope `src/**/*.cs`
- **WHEN** `CommitAsync` is called
- **THEN** no commit is made
- **AND** `CommitHash` is null
- **AND** `FilesStaged` is empty
- **AND** `IsSuccess` is true (not a failure, just no work)

---

### Requirement: Scope intersection staging enforces file boundaries
The system SHALL compute the intersection of changed files and allowed file scopes, staging ONLY files in the intersection.

The algorithm MUST:
- Parse `FileScopes` as glob patterns (e.g., `src/**/*.cs`, `docs/*.md`)
- Compute intersection: `ChangedFiles ∩ Matched(FileScopes)`
- Stage files in deterministic order (sorted by path)
- Never stage files outside the intersection, even if explicitly requested
- Log excluded files with reason ("out of scope")

#### Scenario: Intersection includes only in-scope files
- **GIVEN** `ChangedFiles: ["src/A.cs", "src/B.cs", "tests/T.cs"]`
- **AND** `FileScopes: ["src/**/*.cs"]`
- **WHEN** staging is computed
- **THEN** only `src/A.cs` and `src/B.cs` are staged
- **AND** `tests/T.cs` is logged as excluded (out of scope)

#### Scenario: Glob patterns match correctly
- **GIVEN** `ChangedFiles: ["src/models/User.cs", "src/api/Controller.cs", "README.md"]`
- **AND** `FileScopes: ["src/**/*.cs"]`
- **WHEN** staging is computed
- **THEN** both `src/models/User.cs` and `src/api/Controller.cs` are staged
- **AND** `README.md` is excluded

#### Scenario: Forbidden files never staged
- **GIVEN** an attempt to directly stage `.aos/state/state.json` (system file)
- **WHEN** the committer processes the request
- **THEN** the system file is excluded from staging
- **AND** a warning is logged about the exclusion

---

### Requirement: TSK-based commit message format
The system SHALL generate deterministic commit messages using the format `TSK-XXXXXX: <summary>`.

The message format MUST:
- Start with the canonical task ID (e.g., `TSK-0001`)
- Include a colon and space separator
- Include the provided summary text
- Truncate summary if total message exceeds 72 characters (git convention)

#### Scenario: Commit message includes task ID and summary
- **GIVEN** task ID `TSK-0042` and summary `Add user authentication service`
- **WHEN** the commit is created
- **THEN** the commit message is `TSK-0042: Add user authentication service`

#### Scenario: Long summary is truncated
- **GIVEN** task ID `TSK-0001` and summary exceeding 60 characters
- **WHEN** the commit is created
- **THEN** the message is truncated to fit within 72 characters total
- **AND** truncation is indicated with ellipsis

---

### Requirement: Commit evidence is captured per run
The system SHALL write commit metadata to per-run artifacts at `.aos/evidence/runs/<run-id>/artifacts/`.

The committer MUST write:
- `git-diffstat.json`: Computed before commit, contains `filesChanged`, `insertions`, `deletions`
- `git-commit.json`: Written after commit, contains `commitHash`, `message`, `timestamp`, `taskId`

Both files MUST:
- Use deterministic JSON serialization per `aos-deterministic-json-serialization`
- Include `schemaVersion: 1`
- Be written atomically (no partial/corrupt artifacts)

#### Scenario: Run artifacts contain commit metadata
- **GIVEN** a successful commit for task TSK-0001 in run RUN-0042
- **WHEN** examining `.aos/evidence/runs/RUN-0042/artifacts/`
- **THEN** `git-diffstat.json` exists with correct stats
- **AND** `git-commit.json` exists with hash, message, timestamp, taskId

#### Scenario: No commit produces null hash in evidence
- **GIVEN** an execution with empty intersection (no files to commit)
- **WHEN** examining the run artifacts
- **THEN** `git-commit.json` contains `commitHash: null`
- **AND** `git-diffstat.json` contains `filesChanged: 0`

---

### Requirement: Task evidence pointers are updated
The system SHALL update task-evidence `latest.json` pointers with commit metadata upon successful completion.

The committer MUST:
- Update `.aos/evidence/task-evidence/<task-id>/latest.json`
- Populate `gitCommit` slot with commit hash (or null if no commit)
- Populate `diffstat` slot with computed diff statistics
- Use atomic file writes per `aos-deterministic-json-serialization`

#### Scenario: Task evidence updated after commit
- **GIVEN** a successful commit for task TSK-0001 with hash `abc1234`
- **AND** diffstat showing 3 files changed, 50 insertions, 10 deletions
- **WHEN** examining `.aos/evidence/task-evidence/TSK-0001/latest.json`
- **THEN** `gitCommit` is `"abc1234"`
- **AND** `diffstat.filesChanged` is 3
- **AND** `diffstat.insertions` is 50
- **AND** `diffstat.deletions` is 10

#### Scenario: Null commit reflected in task evidence
- **GIVEN** an execution where no files were staged
- **WHEN** examining `.aos/evidence/task-evidence/<task-id>/latest.json`
- **THEN** `gitCommit` is `null`
- **AND** `diffstat` reflects zero changes

---

### Requirement: Handler integrates with orchestrator gating
The system SHALL provide an `AtomicGitCommitterHandler` that integrates with the orchestrator's gating and dispatch system.

The handler MUST:
- Implement the handler pattern used by `agents-orchestrator-workflow`
- Accept a commit intent with task reference and execution results
- Gate: verify non-empty intersection before proceeding (fail fast if empty)
- Delegate to `IAtomicGitCommitter` for actual commit
- Return `HandlerResult` indicating success/failure and next phase
- Route to Verifier phase on success, FixPlanner on failure

#### Scenario: Handler routes to Verifier on success
- **GIVEN** a commit intent for task TSK-0001 with valid files to stage
- **WHEN** the handler is invoked by the orchestrator
- **THEN** it executes the commit
- **AND** returns success with next phase "Verifier"

#### Scenario: Handler routes to FixPlanner on empty intersection
- **GIVEN** a commit intent where no changed files match the allowed scope
- **WHEN** the handler is invoked
- **THEN** it returns failure with next phase "FixPlanner"
- **AND** error message indicates "No files to commit in scope"

#### Scenario: Handler routes to FixPlanner on git error
- **GIVEN** a commit intent where git command fails (e.g., not a git repo)
- **WHEN** the handler is invoked
- **THEN** it returns failure with next phase "FixPlanner"
- **AND** error message includes git stderr

---

## Related Specifications
- `aos-task-evidence`: Defines `gitCommit` and `diffstat` slots in task-evidence pointers
- `agents-task-executor`: Provides file scope enforcement patterns and `TaskExecutionResult`
- `aos-evidence-store`: Defines artifact storage conventions
- `aos-deterministic-json-serialization`: Required for evidence file formatting
- `aos-run-lifecycle`: Run record creation and management
- `agents-orchestrator-workflow`: Handler pattern and phase routing
