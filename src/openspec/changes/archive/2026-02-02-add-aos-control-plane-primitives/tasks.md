## 1. Config system (`.aos/config/**`)
- [x] 1.1 Define schemas for config documents in the local schema pack and register them in `.aos/schemas/registry.json`.
- [x] 1.2 Implement config loader + validator (secrets-by-reference only).
- [x] 1.3 Add CLI surface: `aos config validate` (and help/usage).
- [x] 1.4 Add workspace validation rule: if `.aos/config/config.json` exists, it MUST validate (do not require config for baseline workspace validity yet).

## 2. Lock manager (`.aos/locks/**`)
- [x] 2.1 Define lock artifact format and contract path(s) under `.aos/locks/**`.
- [x] 2.2 Implement exclusive workspace lock acquisition for all mutating commands (init, run start/finish, execute-plan, repair indexes, checkpoint operations).
- [x] 2.3 Add CLI surface: `aos lock status`, `aos lock acquire`, `aos lock release` (minimum viable operability).
- [x] 2.4 Ensure lock contention returns a stable exit code and actionable error output.

## 3. Checkpoints (`.aos/state/checkpoints/**`)
- [x] 3.1 Define checkpoint artifact structure and schemas (checkpoint metadata + state snapshot).
- [x] 3.2 Implement `aos checkpoint create` (snapshot) and `aos checkpoint restore` (rollback), writing a state event describing the action.
- [x] 3.3 Ensure checkpoint operations are protected by the workspace lock.

## 4. State transition engine
- [x] 4.1 Define minimal state transition table for the current cursor model (including rollback rules that require a checkpoint).
- [x] 4.2 Ensure state writes only occur through validated transitions (reject invalid transitions without partial writes).

## 5. Normalized errors + stable CLI exit codes
- [x] 5.1 Define a normalized error envelope type (code/message/details) and map engine exceptions to stable error codes.
- [x] 5.2 Update CLI command handlers to use stable exit codes and to print actionable errors consistently.
- [x] 5.3 Add tests for representative failure modes (invalid usage, validation failure, policy violation, lock contention).

## 6. Specialist agent IO contracts
- [x] 6.1 Define request/result schemas and their deterministic write rules.
- [x] 6.2 Implement evidence writers for agent request/result artifacts (even if no full orchestration is implemented yet).

## 7. Policy enforcement gates
- [x] 7.1 Define policy schema (scope allowlist, tool allowlist, and no-implicit-state checks).
- [x] 7.2 Enforce policy in `execute-plan` (and any future agent execution): reject violations with stable exit code and normalized error.

## 8. Run packet/result artifacts
- [x] 8.1 Extend run lifecycle to write `.aos/evidence/runs/<run-id>/packet.json` at run start.
- [x] 8.2 Extend run lifecycle to write `.aos/evidence/runs/<run-id>/result.json` at run finish (including errors and produced artifact references).
- [x] 8.3 Update `execute-plan` to populate packet/result fields relevant to plan execution.
- [x] 8.4 Add golden fixtures/tests for packet/result determinism boundaries.

## 9. Provider/tool call envelopes (audit + replay contracts)
- [x] 9.1 Define call envelope schema and log location under `.aos/evidence/runs/<run-id>/logs/**`.
- [x] 9.2 Implement minimal envelope logger abstraction and a “replay-disabled” runtime path (record-only).

## 10. Validation
- [x] 10.1 Add/extend unit tests in `tests/nirmata.Aos.Tests` for new contract artifacts and CLI behaviors.
- [x] 10.2 Add deterministic snapshot/golden fixtures where appropriate.

