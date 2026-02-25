# Tasks: Remediate Core Reliability Defects

## 1. Lock Manager Race Condition Remediation
- [x] MODIFIED `aos-lock-manager` spec with atomic release requirements <!-- id: 1.1 -->
- [x] Update `AosWorkspaceLockManager` to use `FileShare.None` file-handle persistence <!-- id: 1.2 -->
- [x] Update `AosWorkspaceLockHandle.Dispose` to close the held handle rather than re-reading from disk <!-- id: 1.3 -->
- [x] Add E2E lock contention tests with multi-process simulation <!-- id: 1.4 -->

## 2. Path Sanitization & Validation
- [x] MODIFIED `aos-path-routing` spec with strict "jail" validation requirements <!-- id: 2.1 -->
- [x] Implement `AosPathRouter.ValidateSubpath(string path)` to prevent traversal <!-- id: 2.2 -->
- [x] Update `CacheManager` to use validated path resolution <!-- id: 2.3 -->
- [x] Add unit tests for malicious path traversal attempts <!-- id: 2.4 -->

## 3. Streaming Deterministic Comparison
- [x] MODIFIED `aos-deterministic-json-serialization` spec with performance requirements <!-- id: 3.1 -->
- [x] Implement chunked streaming comparison in `DeterministicJsonFileWriter` <!-- id: 3.2 -->
- [x] Benchmark memory usage for large (10MB+) JSON artifacts <!-- id: 3.3 -->

## 4. Error Handling & Disposal Audit
- [x] MODIFIED `aos-error-model` spec to require diagnostic logging for "best-effort" failures <!-- id: 4.1 -->
- [x] Replace empty catch blocks in `CacheManager`, `AosStateStore`, and `Orchestrator` with logging <!-- id: 4.2 -->
- [x] Audit all Store implementations for `IDisposable` compliance <!-- id: 4.3 -->
- [x] Fix resource leaks in `AosStateStore.AppendEvent` <!-- id: 4.4 -->

## 5. Verification
- [x] Run `openspec validate remediate-core-reliability-defects --strict` <!-- id: 5.1 -->
- [x] Execute targeted reliability suite: `dotnet test --filter Category=Reliability` <!-- id: 5.2 -->
