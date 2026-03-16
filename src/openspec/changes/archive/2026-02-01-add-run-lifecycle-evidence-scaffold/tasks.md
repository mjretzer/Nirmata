## 1. Specification updates
- [x] 1.1 Add new capability delta spec `aos-run-lifecycle` (CLI + evidence structure + index contract).

## 2. Engine implementation: run lifecycle evidence scaffold
- [x] 2.1 Add CLI routing for `aos run start` and `aos run finish`.
- [x] 2.2 Implement run ID generation and routing to `.aos/evidence/runs/<run-id>/`.
- [x] 2.3 Implement deterministic run evidence folder creation (logs/, outputs/, run metadata).
- [x] 2.4 Implement deterministic run metadata index under `.aos/evidence/runs/index.json`.
- [x] 2.5 Place evidence-writing logic under `nirmata.Aos/Engine/Evidence/` (namespace `nirmata.Aos.Engine.Evidence`).

## 3. Tests
- [x] 3.1 Add tests proving `aos run start` creates the expected folder structure and index entry.
- [x] 3.2 Add tests proving `aos run finish` updates run metadata and index deterministically.

## 4. Developer ergonomics
- [x] 4.1 Update CLI help output to include new `run` commands and options.
- [x] 4.2 Ensure CI runs the new tests.

