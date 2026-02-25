# Tasks: Harden Orchestrator Governance

## 1. Workspace Lock Manager
- [x] 1.1 Implement `ILockManager` interface with acquire/release/status methods
- [x] 1.2 Implement file-based lock backend at `.aos/locks/workspace.lock`
- [x] 1.3 Add lock contention detection with actionable error messages
- [x] 1.4 Add `aos lock status` command
- [x] 1.5 Add `aos lock acquire` command
- [x] 1.6 Add `aos lock release [--force]` command
- [x] 1.7 Extend all mutating commands to acquire lock at entry
- [x] 1.8 Ensure validation commands bypass lock requirement
- [x] 1.9 Add unit tests for lock acquisition/release/contention
- [x] 1.10 Add integration tests for concurrent command execution

## 2. Run Abandonment Detection and Cleanup
- [x] 2.1 Add `abandoned` status to run lifecycle spec
- [x] 2.2 Add `abandonmentTimeoutMinutes` to `.aos/config/run-lifecycle.json` (default 1440 = 24h)
- [x] 2.3 Implement abandonment detection in run lifecycle (check startedAtUtc vs current time)
- [x] 2.4 Implement `MarkRunAbandoned` method in run lifecycle service
- [x] 2.5 Add background cleanup task in `Gmsd.Windows.Service` to mark abandoned runs
- [x] 2.6 Add `aos cache prune --abandoned` command to manually clean abandoned runs
- [x] 2.7 Update run index to reflect abandoned status
- [x] 2.8 Add unit tests for abandonment detection
- [x] 2.9 Add integration tests for background cleanup task
- [x] 2.10 Document abandonment timeout configuration

## 3. Pause/Resume with User-Visible Status
- [x] 3.1 Add `paused` status to run state schema
- [x] 3.2 Implement `PauseRun` command in continuity plane
- [x] 3.3 Implement `ResumeRun` command in continuity plane
- [x] 3.4 Update `report-progress` to include run status (running/paused/abandoned)
- [x] 3.5 Add `aos run pause --run-id <id>` command
- [x] 3.6 Add `aos run resume --run-id <id>` command
- [x] 3.7 Update run.json to include `status` field (started/paused/resumed/finished/abandoned)
- [x] 3.8 Add validation: pause only works on running runs; resume only on paused runs
- [x] 3.9 Add unit tests for pause/resume state transitions
- [x] 3.10 Add UI component to display and control pause/resume status
- [x] 3.11 Add integration tests for pause/resume with task executor

## 4. Rate Limiting and Concurrency Bounds
- [x] 4.1 Add concurrency configuration schema to `.aos/config/concurrency.json`
- [x] 4.2 Implement `IConcurrencyLimiter` interface
- [x] 4.3 Implement task queue with `maxParallelTasks` limit
- [x] 4.4 Implement LLM call rate limiting with `maxParallelLlmCalls` limit
- [x] 4.5 Add queue size validation (`taskQueueSize`)
- [x] 4.6 Integrate rate limiting into task executor
- [x] 4.7 Integrate rate limiting into LLM provider adapter
- [x] 4.8 Add metrics: queue depth, active tasks, LLM call latency
- [x] 4.9 Add unit tests for rate limiting enforcement
- [x] 4.10 Add integration tests for queue overflow handling
- [x] 4.11 Document concurrency configuration and tuning guidance

## 5. Secret Management
- [x] 5.1 Implement `ISecretStore` abstraction
- [x] 5.2 Implement OS keychain backend (Windows Credential Manager)
- [x] 5.3 Implement test/mock backend for unit tests
- [x] 5.4 Update configuration schema: secrets referenced by name, not value
- [x] 5.5 Add `aos secret set <name> <value>` command
- [x] 5.6 Add `aos secret get <name>` command (output masked)
- [x] 5.7 Add `aos secret list` command (show names only, not values)
- [x] 5.8 Add `aos secret delete <name>` command
- [x] 5.9 Migrate existing plaintext API keys to keychain
- [x] 5.10 Update LLM provider configuration to use secret references
- [x] 5.11 Add unit tests for secret store operations
- [x] 5.12 Add integration tests for secret injection into LLM calls
- [x] 5.13 Document secret management workflow and rotation procedure

## 6. Verification and Testing
- [x] 6.1 Run full test suite for lock manager
- [x] 6.2 Run full test suite for run abandonment
- [x] 6.3 Run full test suite for pause/resume
- [x] 6.4 Run full test suite for rate limiting
- [x] 6.5 Run full test suite for secret management
- [x] 6.6 Add end-to-end test: long-running session with pause/resume
- [x] 6.7 Add end-to-end test: concurrent runs with lock contention
- [x] 6.8 Add end-to-end test: abandoned run detection and cleanup
- [x] 6.9 Add end-to-end test: rate limiting under load
- [x] 6.10 Add end-to-end test: secret injection and rotation
- [x] 6.11 Validate openspec strict compliance
- [x] 6.12 Document verification results in verification-notes.md

## 7. Documentation and Rollout
- [x] 7.1 Update README with hardening features
- [x] 7.2 Add troubleshooting guide for lock contention
- [x] 7.3 Add troubleshooting guide for abandoned runs
- [x] 7.4 Add user guide for pause/resume
- [x] 7.5 Add configuration guide for rate limiting
- [x] 7.6 Add security guide for secret management
- [x] 7.7 Create migration guide for existing deployments
- [x] 7.8 Update CI/CD to test all hardening scenarios
