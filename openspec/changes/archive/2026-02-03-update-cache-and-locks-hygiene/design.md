## Context
This change formalizes hygiene for two operational workspace areas:

- **Cache**: `.aos/cache/**` is explicitly non-authoritative and MUST be safe to delete/prune without breaking core workflows.
- **Locks**: `.aos/locks/workspace.lock.json` is the exclusive workspace lock artifact used to prevent concurrent mutation of `.aos/**`.

The lock file name is **`.json`** to match existing engine contracts, schema, and tests.

## Goals / Non-Goals
- **Goals**
  - Provide an ergonomic CLI surface to manage cache: `aos cache clear` and `aos cache prune`.
  - Ensure cache hygiene operations do not compromise `aos validate ...` or engine correctness.
  - Keep lock contention behavior stable and actionable for humans and automation.

- **Non-Goals**
  - Defining authoritative storage under `.aos/cache/**` (cache remains disposable and non-contractual in content).
  - Introducing cross-process lock leases/renewal protocols (current file-based exclusive lock is sufficient for this milestone).
  - Requiring deterministic outputs for cache prune/clear beyond exit codes and scoped effects (cache hygiene is operational).

## Decisions
### Decision: Cache commands are mutating and require the workspace lock
`aos cache clear` and `aos cache prune` mutate `.aos/**` and therefore SHALL acquire the exclusive workspace lock (same rule as other mutating CLI commands).

### Decision: Cache hygiene is scope-limited to `.aos/cache/**`
- `clear` operates only under `.aos/cache/**` and MUST NOT delete the `.aos/cache/` directory itself.
- `prune` operates only under `.aos/cache/**` and removes entries older than \(N\) days based on filesystem timestamps (default \(N = 30\)).
- Neither command may read/modify `.aos/spec/**`, `.aos/state/**`, or `.aos/evidence/**`.

### Decision: Keep the canonical lock contract path unchanged
The canonical workspace lock contract path is `.aos/locks/workspace.lock.json` (already centralized by `aos-path-routing`).

## Risks / Trade-offs
- **Timestamp-based pruning is not deterministic**: that is acceptable because cache hygiene is operational and non-authoritative; the spec will constrain scope + safety, not byte-for-byte outputs.
- **Over-pruning**: avoid by defaulting prune to a conservative age (30 days) and supporting `--days` override.

## Verification strategy (implementation stage)
- **Lock determinism**: second lock acquisition fails with exit code 4 and actionable details.
- **Cache hygiene safety**:
  - After `aos cache clear`, `aos validate workspace` still runs (pass/fail depends only on validation state).
  - `aos cache prune --days 0` removes all cache entries without impacting validation/spec/state/evidence behavior.

