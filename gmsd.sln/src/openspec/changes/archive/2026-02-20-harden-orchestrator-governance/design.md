# Design: Harden Orchestrator Governance

## Context
The GMSD orchestrator currently lacks production-grade safety guarantees. Long-running sessions can:
- Leave unfinished runs in limbo when processes crash
- Corrupt state through concurrent writes
- Expose secrets in plaintext
- Execute unbounded parallel work without resource limits

This design addresses five hardening concerns: crash safety, concurrency, resumability, rate limiting, and secrets.

## Goals
- **Crash safety:** Abandoned runs are detectable, recoverable, and cleanable
- **Concurrency safety:** Exclusive locks prevent state corruption; contention fails fast
- **Resumability:** Pause/resume are explicit state transitions with user-visible status
- **Resource bounds:** Configurable limits on parallel execution and LLM calls
- **Secret security:** Credentials stored securely; rotation supported; plaintext eliminated

## Non-Goals
- Distributed locking (single-machine workspace assumed)
- Automatic recovery (abandoned runs are marked; recovery is manual or operator-driven)
- Encryption at rest (secrets stored in OS keychain; encryption is OS responsibility)
- Multi-user workspace isolation (single-user per workspace assumed)

## Decisions

### 1. Run Abandonment Strategy
**Decision:** Unfinished runs are marked "abandoned" after a configurable timeout (default 24 hours). Cleanup is deterministic and safe.

**Rationale:**
- Timeout-based detection avoids complex heartbeat machinery
- Marking (not deletion) preserves evidence for debugging
- Deterministic cleanup prevents orphaned state
- Configurable timeout allows tuning for different workload patterns

**Alternatives:**
- Heartbeat-based detection: requires background thread; more complex; harder to test
- Immediate cleanup on process exit: loses evidence; doesn't handle hard crashes

### 2. Workspace Locking
**Decision:** Exclusive file-based locks at `.aos/locks/workspace.lock` protect all write operations. Lock contention fails fast with actionable errors.

**Rationale:**
- File locks are OS-native and reliable
- Exclusive semantics prevent concurrent mutations
- Fast-fail avoids unbounded waiting and deadlocks
- Actionable errors (lock holder, path, next steps) aid troubleshooting

**Alternatives:**
- Advisory locks: weaker guarantees; easier to bypass accidentally
- Distributed locks (Redis, etc.): adds external dependency; overkill for single-machine
- Optimistic concurrency (version numbers): complex; requires full state versioning

### 3. Pause/Resume Status Visibility
**Decision:** Pause/resume commands are explicit state transitions. Status is stored in `.aos/state/run.json` and exposed via `report-progress` and UI.

**Rationale:**
- Explicit transitions prevent accidental state mutations
- Centralized status in state store is source of truth
- UI visibility gives users control and feedback
- Compatible with existing continuity plane

**Alternatives:**
- Implicit pause (no command, just stop calling LLM): confusing; no user visibility
- Separate pause/resume files: fragmented state; harder to reason about

### 4. Rate Limiting and Concurrency Bounds
**Decision:** Configurable limits in `.aos/config/concurrency.json`:
- `maxParallelTasks` (default 3)
- `maxParallelLlmCalls` (default 2)
- `taskQueueSize` (default 10)

Enforcement happens in task executor and LLM provider adapter.

**Rationale:**
- Configuration-driven allows tuning without code changes
- Separate limits for tasks and LLM calls reflect different resource constraints
- Queue size prevents unbounded memory growth
- Enforcement at execution layer is transparent to planning

**Alternatives:**
- Hard-coded limits: inflexible; requires code changes
- Dynamic limits based on system load: complex; requires monitoring
- Per-task limits: granular but harder to reason about globally

### 5. Secret Management
**Decision:** Secrets stored in OS keychain (Windows Credential Manager, macOS Keychain, Linux Secret Service). Configuration references secrets by name, not value.

**Rationale:**
- OS keychain is secure and standard
- Eliminates plaintext storage
- Supports rotation (update keychain, restart service)
- Compatible with CI/CD (environment variable injection)

**Alternatives:**
- Encrypted config file: custom crypto; harder to rotate
- Environment variables only: doesn't work for interactive runs
- External vault (HashiCorp, etc.): adds dependency; overkill for single-machine

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| Timeout-based abandonment misses fast crashes | Acceptable: evidence is preserved; operator can investigate |
| File locks fail on network filesystems | Document requirement for local filesystem; validate at init |
| Pause/resume adds UI complexity | Phased rollout: implement backend first, UI second |
| Rate limiting may slow legitimate work | Configurable defaults; document tuning guidance |
| OS keychain integration is platform-specific | Abstract behind `ISecretStore`; provide fallback for testing |

## Migration Plan

### Phase 1: Lock Manager (Week 1)
- Implement exclusive file-based locks
- Extend all mutating commands to acquire lock
- Add `aos lock status/acquire/release` primitives
- Validation: lock contention fails fast; validation commands bypass lock

### Phase 2: Run Abandonment (Week 2)
- Add `abandoned` status to run lifecycle
- Implement timeout detection in run lifecycle
- Add background cleanup task in Windows Service
- Validation: abandoned runs are marked correctly; cleanup is safe

### Phase 3: Pause/Resume Status (Week 3)
- Add `paused` status to run state
- Implement pause/resume commands
- Update `report-progress` to expose status
- Add UI status display
- Validation: pause/resume transitions are explicit; status is accurate

### Phase 4: Rate Limiting (Week 4)
- Add concurrency configuration schema
- Implement task queue with limits
- Implement LLM call rate limiting
- Validation: limits are enforced; queue doesn't overflow

### Phase 5: Secret Management (Week 5)
- Implement `ISecretStore` abstraction
- Add OS keychain backend
- Update configuration schema to reference secrets by name
- Migrate existing plaintext secrets
- Validation: secrets are stored securely; rotation works

## Open Questions
- Should abandoned run cleanup be automatic (background task) or manual (operator command)?
  - **Proposed:** Automatic cleanup after 7 days; operator can force cleanup via `aos cache prune`
- Should pause/resume be per-task or per-run?
  - **Proposed:** Per-run (pause entire workflow); per-task pausing is future work
- Should rate limiting be per-run or global?
  - **Proposed:** Global (shared across all runs); per-run limiting is future work
- Should secret rotation require service restart or be hot-reloadable?
  - **Proposed:** Require restart for safety; hot-reload is future work
