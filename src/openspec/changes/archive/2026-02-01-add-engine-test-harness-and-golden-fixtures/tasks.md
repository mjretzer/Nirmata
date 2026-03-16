## 1. Specification updates
- [x] 1.1 Add `quality-gates` delta spec requiring deterministic engine fixture/snapshot regression coverage (CI fails on drift).

## 2. Fixture corpus and harness (tests)
- [x] 2.1 Define fixture layout under `tests/nirmata.Aos.Tests/Fixtures/**` for engine regression snapshots (approved + inputs as needed).
- [x] 2.2 Add snapshot test(s) for deterministic JSON serialization behavior (fixture compares bytes/text outputs, including trailing LF + no BOM guardrails).
- [x] 2.3 Add snapshot test(s) for path routing behavior (approved mapping from representative IDs to canonical contract paths).
- [x] 2.4 Add snapshot test(s) for schema validation behavior (CLI output/exit codes for representative success and failure cases).

## 3. Canonical regression entrypoint script
- [x] 3.1 Move `scripts/engine-mvp.ps1` to `.aos/fixtures/scripts/engine-mvp.ps1` (canonical location).
- [x] 3.2 Add a compatibility wrapper at `scripts/engine-mvp.ps1` that invokes `.aos/fixtures/scripts/engine-mvp.ps1`.

## 4. Repo hygiene
- [x] 4.1 Update `.gitignore` to ignore runtime `.aos/**` while allowing committed `.aos/fixtures/**` assets.

## 5. Validation
- [x] 5.1 Ensure `dotnet test` executes the new snapshot tests in CI and fails on nondeterministic drift.
