# Tasks: Implement Subagent Execution Loop

## Section 1: Scope Firewall [x]
- [x] Define `IScopeFirewall` interface with `ValidatePath(string path): void` method.
- [x] Implement `ScopedTool` decorator wrapping `ITool` to validate file paths before execution.
- [x] Implement `ScopedToolRegistry` to wrap tools at resolution time.
- [x] Add path normalization logic using `Path.GetFullPath()` to handle relative paths and symlinks.
- [x] Integrate scope firewall into `TaskExecutor` constructor via dependency injection.
- [x] Add unit tests for scope firewall:
  - [x] Positive case: allowed file access succeeds.
  - [x] Negative case: out-of-scope file access throws `ScopeViolationException`.
  - [x] Edge case: symlink traversal is blocked.
  - [x] Edge case: relative path normalization works correctly.

## Section 2: Verification Tools [x]
- [x] Implement `BuildTool` class:
  - [x] Executes `dotnet build` with 5-minute timeout.
  - [x] Returns structured `{ success: bool, logs: string, duration: TimeSpan, errors: string[] }`.
  - [x] Respects scope firewall for build output paths.
- [x] Implement `TestTool` class:
  - [x] Executes `dotnet test --logger:trx` with 10-minute timeout.
  - [x] Parses `.trx` files to extract test results.
  - [x] Returns structured `{ success: bool, totalTests: int, passed: int, failed: int, failures: FailureDetail[], logs: string }`.
- [x] Implement `TrxResultParser` to parse `.trx` XML files into structured data.
- [x] Register `BuildTool` and `TestTool` in `IToolRegistry`.
- [x] Add unit tests for both tools (success, failure, timeout scenarios).

## Section 3: Evidence Enhancement [x]
- [x] Implement `ToolCallingEventLogger` to capture tool calls to `tool-calls.ndjson`:
  - [x] Format: `{ timestamp: ISO8601, toolName: string, input: object, output: object, duration: TimeSpan, success: bool, error?: string }`.
  - [x] Subscribe to `IToolCallingEventEmitter` during loop execution.
- [x] Implement `DiffGenerator` to produce unified diffs:
  - [x] Compare initial state (git HEAD or baseline) with final state.
  - [x] Generate RFC 3881 compliant unified diff format.
  - [x] Ensure deterministic output (consistent line endings, no timestamps).
- [x] Implement `ExecutionSummaryWriter` to generate `execution-summary.json`:
  - [x] Aggregate: task ID, timestamps, iterations, files modified, tool calls, build/test results.
  - [x] Include completion status and deterministic hash.
- [x] Implement `DeterministicHashGenerator`:
  - [x] SHA256 hash of concatenated: `tool-calls.ndjson` + `changes.patch` + `execution-summary.json`.
  - [x] Store hash in both `execution-summary.json` and separate `deterministic-hash` file.
- [x] Update `TaskExecutor` to call all evidence writers after loop completion.
- [x] Add unit tests for evidence generation (format validation, determinism).

## Section 4: Loop Refinement [x]
- [x] Refine `TaskExecutor.ExecuteAsync()` to:
  - [x] Initialize `ScopedToolRegistry` with task's `AllowedFileScope`.
  - [x] Create `ToolCallingLoop` with scoped registry.
  - [x] Execute loop with max iteration limit (e.g., 20 iterations).
  - [x] Detect natural completion: LLM response without tool calls.
  - [x] Handle completion criteria: success, max iterations reached, or error state.
- [x] Enhance error handling:
  - [x] Catch `ScopeViolationException` and return structured error in evidence.
  - [x] Catch tool execution timeouts and report in evidence.
  - [x] Catch LLM provider errors and propagate with context.
- [x] Update `TaskExecutor` to persist evidence to run folder.
- [x] Add E2E tests for `TaskExecutor`:
  - [x] Test: simple file modification task completes successfully.
  - [x] Test: scope violation is caught and reported.
  - [x] Test: build/test tools are called and results captured.
  - [x] Test: evidence artifacts are correctly formatted and persisted.

## Section 5: Verification [x]
- [x] Run `openspec validate implement-subagent-execution --strict`.
- [x] Execute E2E test with a sample task plan (e.g., "Add a new service method").
- [x] Verify evidence artifacts:
  - [x] `tool-calls.ndjson` contains all tool calls with correct format.
  - [x] `changes.patch` is valid unified diff format.
  - [x] `execution-summary.json` is valid JSON with all required fields.
  - [x] `deterministic-hash` matches computed hash of evidence files.
- [x] Verify scope firewall blocks out-of-scope access.
- [x] Create verification notes document with test results and evidence samples.
