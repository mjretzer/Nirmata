## Context
The AOS engine currently provides:
- deterministic JSON serialization
- workspace bootstrap + validation
- run lifecycle evidence scaffolding
- state snapshot + event log stores

However, orchestration is not yet fully operable because:
- there is no validated configuration contract
- there is no concurrency control for mutating commands
- there is no checkpoint/rollback mechanism
- failures are “non-zero” but not normalized/stable
- there is no uniform request/result contract for specialist agents
- policy gates are not explicit artifacts
- run execution lacks packet/result artifacts suitable for replay/resume

This change proposal introduces the minimal “control plane primitives” needed for a deterministic, auditable orchestration loop.

## Goals / Non-Goals
### Goals
- Move critical orchestration decisions into **validated artifacts** under `.aos/**`.
- Make mutating operations **safe under concurrency** via explicit locking.
- Make progress and rollback **auditable** via checkpoints and stable event/state transitions.
- Make failures **machine-readable** with stable exit codes and error envelopes.
- Make run inputs/outputs **replayable** via packet/result artifacts and call envelopes.

### Non-Goals
- Building a full planner/verifier/fix workflow (later milestones).
- Implementing a complete provider ecosystem or a full replay engine (this establishes contracts and minimal scaffolding).
- Supporting multi-project workspaces (explicitly out-of-scope per current workspace invariants).

## Decisions
### Decision: Keep deterministic artifacts separated from operational ephemera
- **Deterministic artifacts**: config documents, run packet/result, checkpoint snapshots, schema-validated documents.
- **Operational ephemera**: lock acquisition metadata (timestamps/PIDs), which may be non-deterministic but must still be well-formed and actionable.

### Decision: Lock scope is “workspace-wide exclusive” for mutating commands
This is the simplest concurrency model and avoids partial writes across multiple artifacts.

### Decision: Exit codes expand from today’s {0,1,2} to a small stable set
We preserve existing meanings and add explicit codes for policy and lock failures to make orchestration operable.

### Decision: Packet/result become first-class run artifacts
- `packet.json` captures **inputs + policy + config snapshot references** for a run.
- `result.json` captures **outcomes + normalized errors + produced artifact references**.

## Risks / Trade-offs
- **More files under `.aos/**`** → mitigated by strict validation and stable contracts.
- **Lock bugs can block progress** → mitigated by TTL/force-release CLI surface and actionable error output.
- **Over-specifying replay too early** → mitigated by keeping replay as “contract-only” and deferring full implementation.

## Migration Plan
- Introduce config/locks/checkpoints as additive layers.
- Gate mutating commands on lock acquisition and workspace/config validation.
- Add packet/result writing to run start/finish (and execute-plan auto-run).
- Add tests and golden fixtures for packet/result determinism boundaries (run ID and timestamps vary; content must be stable otherwise).

## Open Questions
- What is the minimal “secret reference” set to support (env-only vs env+file+vault URIs)?
- Should `packet.json` include raw CLI args or a normalized command descriptor (recommended: both, with normalized first)?
- Should lock TTL be mandatory or optional in v1?

