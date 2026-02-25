# Design: Core Reliability Remediations

## Architectural Reasoning

### 1. Lock Manager Synchronization (Race Condition Elimination)
The current implementation of `AosWorkspaceLockManager` suffers from a TOCTOU (Time-of-Check-To-Use) race condition in its `Dispose` method. While acquisition is atomic via `FileMode.CreateNew`, the release check is not. 

**Solution**:
-   Implement an atomic release mechanism. Instead of `File.Exists` -> `File.ReadAllText` -> `File.Delete`, we will use a shared-access file handle strategy or an OS-level named Mutex (where appropriate for cross-process synchronization on the same machine).
-   For cross-platform reliability, we will prefer `FileStream` with `FileShare.None` held for the duration of the lock's lifetime, ensuring that the OS prevents other processes from acquiring it.

### 2. Path Validation & Sanitization
Ad-hoc path concatenation in `CacheManager` and other stores creates risks for path traversal.

**Solution**:
-   Centralize all path resolution in `AosPathRouter`.
-   Implement strict "jail" validation: all resolved absolute paths MUST be child paths of the `AosRootPath`.
-   Rejects any paths containing `..` or leading slashes that attempt to escape the root.

### 3. Streaming Deterministic JSON Comparison
`DeterministicJsonFileWriter` currently reads the entire existing file into a `byte[]` to check for churn. This is inefficient for large metadata files or evidence logs.

**Solution**:
-   Implement a streaming comparison: open the existing file and the new canonical stream simultaneously and compare them in chunks (e.g., 4KB).
-   Stop early if a mismatch is found, avoiding full memory load for identical large files.

### 4. Structured Logging & Exception Strategy
Empty catch blocks hide critical failures.

**Solution**:
-   Introduce an `IAosDiagnosticLogger` (or use existing `ILogger` where available).
-   Every "best-effort" cleanup operation must log a warning if it fails.
-   Replace generic `catch { }` with `catch (Exception ex) when (IsExpectedNonCritical(ex))` to ensure unexpected crashes are still surfaced.

### 5. Resource Disposal Patterns
Several stores do not consistently use `using` statements or `IDisposable` for file-backed resources.

**Solution**:
-   Ensure all Store implementations that hold state or file handles implement `IDisposable`.
-   Standardize on `using` var patterns for all `FileStream` and `JsonDocument` usage.
