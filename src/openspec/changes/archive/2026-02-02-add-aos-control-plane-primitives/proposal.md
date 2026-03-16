# Change: Add AOS control plane primitives (Milestone E1.5)

## Why
The engine already has deterministic stores, workspace validation, and a minimal run lifecycle, but it lacks the governance primitives needed to operate orchestration safely (config, locking, checkpoints, stable errors, policy gates, and auditable request/result envelopes).

Milestone E1.5 moves orchestration decisions from chat/transient state into **validated, persisted artifacts** so runs can be resumed, audited, and reproduced deterministically.

## What Changes
- Add a **validated config layer** at `.aos/config/**` with **secrets-by-reference only**, plus `aos config ...` CLI surface.
- Add a **workspace lock manager** at `.aos/locks/**` and require locks for any CLI command that mutates `.aos/**`.
- Add a **checkpoint system** under `.aos/state/checkpoints/**` for safe rollback and auditability.
- Add a **state transition engine** with allowed transitions + rollback rules (checkpoint-aware).
- Define a **normalized error model** and **stable CLI exit codes** (beyond “non-zero”).
- Define **specialist agent IO contracts** (uniform request/result shapes + required evidence artifacts).
- Add **policy enforcement gates** (scope allowlist, tool allowlist, and no-implicit-state checks).
- Extend run evidence with canonical **packet/result artifacts**:
  - `.aos/evidence/runs/<run-id>/packet.json`
  - `.aos/evidence/runs/<run-id>/result.json`
- Introduce **provider/tool call envelopes** to support auditing and eventual replay.

## Impact
- **Affected specs (new)**:
  - `aos-config-system`
  - `aos-lock-manager`
  - `aos-checkpoints`
  - `aos-state-transition-engine`
  - `aos-error-model`
  - `aos-agent-io-contracts`
  - `aos-policy-enforcement`
  - `aos-provider-tool-abstractions`
- **Affected specs (modified)**:
  - `aos-run-lifecycle` (adds `packet.json` and `result.json`)
- **Affected code (expected)**:
  - CLI routing and exit codes: `nirmata.Aos/Composition/Program.cs`
  - Evidence writers: `nirmata.Aos/Engine/Evidence/**`
  - Workspace validation gates: `nirmata.Aos/Engine/Validation/**`
  - State store and events: `nirmata.Aos/Engine/Stores/AosStateStore.cs`

## Compatibility / Migration Notes
- Existing workspaces remain valid; config/lock/checkpoint artifacts are introduced as additional layers and commands.
- Mutating commands will begin failing fast when a lock is held (explicitly actionable errors).

