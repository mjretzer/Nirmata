## 1. Specification updates
- [x] 1.1 Add new capability delta spec `aos-execute-plan` (plan file contract + safe output scope + evidence log).

## 2. Engine implementation: minimal planned task executor (E0 “toy”)
- [x] 2.1 Add CLI routing for `aos execute-plan` (help + options + exit codes).
- [x] 2.2 Define and implement plan loader (read + parse + validate minimal plan JSON).
- [x] 2.3 Implement safe path rules for outputs (reject absolute paths and traversal; enforce outputs-only).
- [x] 2.4 Implement executor that writes plan outputs under `.aos/evidence/runs/<run-id>/outputs/**` deterministically.
- [x] 2.5 Implement evidence logging for actions taken under `.aos/evidence/runs/<run-id>/logs/**` (place evidence-writing code under `nirmata.Aos/Engine/Evidence/ExecutePlan/**`).
- [x] 2.6 Auto-start a run at the beginning of `execute-plan` and finish the run on success.

## 3. Tests
- [x] 3.1 Add tests proving `aos execute-plan` writes only to the run outputs folder.
- [x] 3.2 Add tests proving `execute-plan` produces deterministic outputs from identical plan inputs.
- [x] 3.3 Add tests proving an actions log is created and is deterministic.
- [x] 3.4 Add tests covering path traversal attempts are rejected with actionable errors.

## 4. Developer ergonomics
- [x] 4.1 Update CLI help output to include `execute-plan` usage and options.
- [x] 4.2 Ensure CI runs the new tests.

