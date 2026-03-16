# aos-cache-hygiene Specification

## Purpose

Defines canonical AOS workspace contracts and behavioral semantics for $capabilityId.

- **Lives in:** `nirmata.Aos/*`, `.aos/**`
- **Owns:** Engine-level artifact contracts, validation, and deterministic IO semantics for this capability
- **Does not own:** Plane/orchestrator workflows (owned by `agents-*` capabilities)
## Requirements
### Requirement: Cache contents are disposable and scope-limited to `.aos/cache/**`
The system SHALL treat `.aos/cache/**` as non-authoritative, disposable workspace data.

Cache clear/prune operations MUST only affect `.aos/cache/**` and MUST NOT modify artifacts under:
- `.aos/spec/**`
- `.aos/state/**`
- `.aos/evidence/**`

#### Scenario: Clearing cache does not impact workspace validation
- **GIVEN** a valid initialized workspace
- **WHEN** `aos cache clear` is executed
- **THEN** `aos validate workspace` still runs normally (pass/fail depends only on validation results)

### Requirement: `aos cache clear` removes cache contents but keeps the cache directory
The system SHALL provide `aos cache clear` to remove all cache entries under `.aos/cache/**`.

The command MUST preserve the `.aos/cache/` directory (it MUST exist after the command completes successfully).

#### Scenario: Cache clear removes entries under `.aos/cache/**`
- **GIVEN** `.aos/cache/` contains one or more files or subdirectories
- **WHEN** `aos cache clear` is executed
- **THEN** the cache entries are removed
- **AND** `.aos/cache/` still exists as a directory

### Requirement: `aos cache prune` removes cache entries older than N days
The system SHALL provide `aos cache prune` to remove cache entries under `.aos/cache/**` that are older than \(N\) days.

- Default \(N\) is **30** days.
- The user MAY override \(N\) using `--days <n>` (non-negative integer).

#### Scenario: Cache prune removes entries older than the threshold
- **GIVEN** `.aos/cache/` contains entries older than 30 days
- **WHEN** `aos cache prune` is executed
- **THEN** the entries older than 30 days are removed
- **AND** `.aos/cache/` still exists as a directory

#### Scenario: Cache prune with days 0 removes all cache entries
- **GIVEN** `.aos/cache/` contains one or more entries
- **WHEN** `aos cache prune --days 0` is executed
- **THEN** all cache entries are removed
- **AND** `.aos/cache/` still exists as a directory

### Requirement: Cache commands require the exclusive workspace lock
The system SHALL require the exclusive workspace lock (per `aos-lock-manager`) for `aos cache clear` and `aos cache prune`.

#### Scenario: Cache clear fails fast when the workspace lock is held
- **GIVEN** the workspace lock is held by another process
- **WHEN** `aos cache clear` is executed
- **THEN** the command fails with the stable lock-contention exit code and actionable lock-holder details

#### Scenario: Cache prune fails fast when the workspace lock is held
- **GIVEN** the workspace lock is held by another process
- **WHEN** `aos cache prune` is executed
- **THEN** the command fails with the stable lock-contention exit code and actionable lock-holder details

