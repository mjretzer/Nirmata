## 1. Specification updates
- [x] 1.1 Add new capability delta spec `aos-workspace-bootstrap` defining `aos init` behavior, required paths, determinism, and idempotency.

## 2. Engine implementation (library + CLI)
- [x] 2.1 Add an engine workspace bootstrapper in `Gmsd.Aos` that creates/verifies the `.aos/*` skeleton and writes baseline artifacts.
- [x] 2.2 Implement the `aos init` command wiring (argument parsing + handler) in `Gmsd.Aos` and invoke the bootstrapper at repository root.
- [x] 2.3 Ensure `aos init` is safe to re-run (idempotent) and fails with actionable diagnostics when `.aos/` exists but is non-compliant.

## 3. Validation and regression proof
- [x] 3.1 Add a golden fixture test that runs `aos init` in a temp workspace and asserts the produced `.aos/*` tree matches an approved fixture.
- [x] 3.2 Add developer docs for running `aos init` locally (including any tool install steps, if applicable).
- [x] 3.3 Ensure CI executes the fixture test to prove determinism across machines.

