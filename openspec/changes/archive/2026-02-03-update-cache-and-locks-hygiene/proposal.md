# Change: Update cache + locks hygiene

## Why
The AOS workspace needs **safe operational hygiene**:

- `.aos/cache/**` MUST be disposable so clearing/pruning does not break validation, spec, state, or evidence flows.
- `.aos/locks/workspace.lock.json` MUST be concurrency-safe and actionable so concurrent workspace mutation fails fast and predictably.

## What Changes
- Add `aos cache clear` and `aos cache prune` CLI commands for managing `.aos/cache/**`.
  - `clear`: remove cache contents while keeping `.aos/cache/` present.
  - `prune`: age-based removal of cache entries older than \(N\) days (default: 30).
- Treat cache clear/prune as **mutating commands** and require the exclusive workspace lock (consistent with `aos-lock-manager`).
- Clarify the lock-manager surface contract around the lock CLI primitives (`aos lock status|acquire|release`) and deterministic contention behavior.

## Impact
- **Affected specs**
  - `aos-lock-manager` (delta: lock CLI surface contract and contention expectations)
  - `aos-path-routing` (reference: canonical lock contract path is already defined there)
  - `aos-error-model` (reference: lock contention exit code is already defined there)
  - New: `aos-cache-hygiene` (delta: cache clear/prune behavior contract)
- **Affected code (implementation stage)**
  - `Gmsd.Aos/Composition/Program.cs` (new `cache` command group)
  - New engine cache module (e.g., `Gmsd.Aos/Engine/Cache/**`) to implement clear/prune behaviors
  - Tests under `tests/Gmsd.Aos.Tests` for cache clear/prune + lock contention behavior

