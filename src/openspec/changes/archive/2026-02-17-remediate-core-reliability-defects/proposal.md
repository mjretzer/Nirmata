# Proposal: Remediate Core Reliability Defects

## Problem Statement
The nirmata core infrastructure contains several reliability defects identified during a comprehensive code review. These include a critical race condition in the lock manager, inconsistent error handling (swallowed exceptions), missing path sanitization, and potential resource leaks in state and cache management. These issues undermine the stability and determinism required for a reliable agentic orchestrator.

## Proposed Changes
1. **Lock Manager Synchronization**: Implement robust synchronization primitives (e.g., OS-level named mutexes or atomic file operations with retry) to eliminate the TOCTOU race condition in `AosWorkspaceLockManager`.
2. **Comprehensive Error Handling & Logging**: Systematically replace empty catch blocks with structured logging and actionable error reporting, particularly in cleanup and background operations.
3. **Path Validation & Sanitization**: Enforce strict path validation in `AosPathRouter` and downstream consumers to prevent path traversal and ensure platform-neutral behavior.
4. **Streaming Deterministic JSON Comparison**: Optimize `DeterministicJsonFileWriter` to use streaming comparison for large files to avoid memory pressure and churn.
5. **Resource Management & Disposal**: Audit and implement proper `IDisposable` patterns for all file and stream-based resources.

## Expected Impact
- **Increased Stability**: Elimination of race conditions and resource leaks.
- **Improved Observability**: Better diagnostic data from structured logging.
- **Enhanced Security**: Prevention of path traversal vulnerabilities.
- **Better Performance**: Reduced memory overhead for large JSON artifacts.

## Related Specs
- `aos-lock-manager`
- `aos-error-model`
- `aos-path-routing`
- `aos-deterministic-json-serialization`
- `aos-cache-hygiene`
- `aos-state-store`
