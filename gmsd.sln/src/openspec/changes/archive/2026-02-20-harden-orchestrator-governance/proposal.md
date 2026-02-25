# Change: Harden Orchestrator Governance

## Why
Long-running agent sessions are vulnerable to state corruption, unrecoverable failures, and security risks. The system currently lacks:
- Crash-safe finalization (abandoned runs accumulate indefinitely)
- Workspace concurrency protection (multiple processes can corrupt state simultaneously)
- Resumability guarantees (pause/resume lacks explicit user-visible status)
- Rate limiting and concurrency bounds (unbounded parallel execution)
- Secret handling (API keys stored in plaintext, no rotation mechanism)

These gaps make the orchestrator unsuitable for production use and prevent safe, long-running sessions.

## What Changes
- **Crash-safe run finalization:** Unfinished runs are marked "abandoned" after configurable timeout; cleanup is deterministic and safe
- **Workspace locks for write operations:** Exclusive locks prevent concurrent state mutations; lock contention fails fast with actionable errors
- **Resumability with user-visible status:** Pause/resume commands expose explicit state transitions; UI displays run status (running/paused/abandoned)
- **Rate limiting and concurrency bounds:** Configurable limits on parallel task execution and LLM provider calls
- **Secret handling:** API keys stored in OS keychain or secure vault; rotation via configuration; plaintext storage eliminated

## Impact
- **Affected specs:**
  - `aos-run-lifecycle` (MODIFIED: add abandoned status and timeout semantics)
  - `aos-lock-manager` (MODIFIED: extend to cover all write operations)
  - `agent-continuity` (MODIFIED: add pause/resume status visibility)
  - NEW: `aos-run-abandonment` (crash-safe finalization)
  - NEW: `aos-workspace-concurrency` (concurrency bounds and rate limiting)
  - NEW: `secret-management` (secure credential storage and rotation)

- **Affected code:**
  - `Gmsd.Aos/*` (run lifecycle, lock manager, secret store)
  - `Gmsd.Agents/*` (continuity plane, rate limiting, secret injection)
  - `Gmsd.Windows.Service/*` (background cleanup for abandoned runs)
  - `Gmsd.Web/*` (UI status display for pause/resume)

- **Breaking changes:**
  - Run lifecycle commands now require exclusive workspace lock
  - Pause/resume commands replace ad-hoc state mutations with explicit state transitions
  - Secret configuration schema changes (plaintext keys no longer supported)
