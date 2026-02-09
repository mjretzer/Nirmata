# Design: Atomic Git Committer

## Overview
The Atomic Git Committer is a scope-aware, deterministic git workflow that ensures task executions leave traceable, auditable commits while preventing scope creep. It bridges the gap between task execution (which modifies files) and evidence capture (which needs to know what changed).

## Architectural Decisions

### 1. Scope-First Staging Philosophy
**Decision:** Compute intersection(changed files, allowed scope) before any git operations.

**Rationale:**
- Prevents accidental commits of out-of-scope files
- Aligns with `agents-task-executor` file scope enforcement
- Makes "what was intended to change" vs "what actually changed" explicit

**Trade-off:** Requires accurate file scope definitions in task plans. Poor scoping leads to empty intersections and no-ops.

### 2. Evidence-Before-Git Ordering
**Decision:** Compute diffstat and prepare evidence slots BEFORE executing git commit.

**Rationale:**
- If git fails (e.g., network, locks), we still have the diffstat of what was staged
- Evidence is always consistent even if git operation is retried
- Supports idempotent re-runs (same diffstat, potentially different commit hash)

### 3. TSK-Based Commit Message Convention
**Decision:** Use `TSK-XXXXXX: <summary>` format for all task-scoped commits.

**Rationale:**
- Creates explicit traceability from commit to task
- Enables automated changelog generation
- Human-readable while machine-parseable

**Alternatives considered:**
- Run IDs: Too volatile for human inspection
- Free-form: Loses traceability guarantees

### 4. Dual Evidence Path
**Decision:** Write commit metadata to BOTH per-run artifacts AND task-evidence pointers.

**Rationale:**
- Per-run artifacts (`git-commit.json`) provide full audit trail per execution attempt
- Task-evidence pointers (`latest.json`) provide fast "what is current" lookups
- Supports both operational queries ("what did this run do?") and state queries ("what is the latest for this task?")

### 5. Handler Integration Pattern
**Decision:** Implement as `IHandler` that integrates with the orchestrator's gating system.

**Rationale:**
- Git commit is a "terminal" phase action (runs after task execution, before verification)
- Gating allows pre-commit validation (e.g., scope re-check, evidence completeness)
- Consistent with `agents-task-executor` handler pattern

## Component Structure

```
Gmsd.Agents.Execution.Execution.AtomicGitCommitter/
├── IAtomicGitCommitter.cs          # Interface contract
├── AtomicGitCommitter.cs           # Core implementation
├── AtomicGitCommitterHandler.cs    # Orchestrator integration
├── Models/
│   ├── CommitRequest.cs            # Input: taskId, scope, summary
│   ├── CommitResult.cs               # Output: hash, diffstat, success/failure
│   └── StagingIntersection.cs        # Internal: computed file set
└── Evidence/
    ├── CommitEvidenceWriter.cs       # Writes git-commit.json artifacts
    └── TaskEvidenceUpdater.cs        # Updates latest.json pointers
```

## Data Flow

1. **Task Completion** → Executor signals success with modified files
2. **Intersection Compute** → ChangedFiles ∩ AllowedScope = FilesToStage
3. **Pre-Stage Evidence** → Write `git-diffstat.json` with computed stats
4. **Git Stage** → `git add` only FilesToStage
5. **Git Commit** → `git commit -m "TSK-XXXXXX: summary"`
6. **Post-Commit Evidence** → Write `git-commit.json` with hash
7. **Task Evidence Update** → Update `latest.json` gitCommit and diffstat slots
8. **Orchestrator Signal** → Return CommitResult to handler for next phase routing

## Integration Points

### With agents-task-executor
- Consumes `TaskExecutionResult.FilesModified` as the "changed files" input
- Uses same `fileScopes` from task plan for allowed scope
- Runs after successful task execution (terminal phase)

### With aos-task-evidence
- Writes to `gitCommit` and `diffstat` slots in `latest.json`
- Uses deterministic JSON serialization per `aos-deterministic-json-serialization`

### With aos-evidence-store
- Creates per-run artifacts in `.aos/evidence/runs/RUN-*/artifacts/`
- Artifacts cataloged in run manifest

### With orchestrator workflow
- Handler implements gating: checks intersection non-empty before proceeding
- Routes to Verifier on success, FixPlanner on failure (no files staged, git error)

## Safety Mechanisms

### Forbidden File Protection
- Files outside scope are never staged, even if explicitly requested
- Violation is logged but does not fail the commit (other files proceed)
- Evidence records which files were excluded and why

### Empty Intersection Handling
- If no changed files match scope, no commit is made
- Evidence records `gitCommit: null` and `filesChanged: 0`
- Handler routes to FixPlanner with descriptive error ("No files to commit in scope")

### Git Error Handling
- Git command failures are captured in evidence with exit code and stderr
- No partial evidence writes (atomic JSON writes per `aos-deterministic-json-serialization`)
- Handler receives failure result for routing decisions

## Determinism Considerations

- Commit timestamps are captured but not considered part of the "deterministic" outcome
- Diffstat is deterministic based on staged content
- Commit hash may vary if git user config differs (acceptable variance)
- Evidence JSON uses canonical deterministic formatting
