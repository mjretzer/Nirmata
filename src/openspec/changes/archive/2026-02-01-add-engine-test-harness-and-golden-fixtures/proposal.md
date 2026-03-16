# Change: add-engine-test-harness-and-golden-fixtures

## Why
We need a deterministic regression harness for the AOS engine so drift (JSON formatting, path routing, schema validation behavior) is caught automatically and consistently across machines and CI.

## What Changes
- Introduce a versioned golden-fixture corpus for AOS engine regression testing.
- Add snapshot-style tests that compare produced artifacts/outputs against approved fixtures and fail on any nondeterministic drift.
- Establish the canonical regression entrypoint script at `.aos/fixtures/scripts/engine-mvp.ps1`.
- Keep backwards compatibility by retaining a wrapper at `scripts/engine-mvp.ps1`.

## Impact
- **Affected specs**: `quality-gates` (new deterministic fixture/snapshot gate for the AOS engine).
- **Affected code (later apply stage)**:
  - Test harness and fixtures under `tests/nirmata.Aos.Tests/Fixtures/**`
  - Regression script location: `.aos/fixtures/scripts/engine-mvp.ps1` (plus wrapper at `scripts/engine-mvp.ps1`)
  - CI: ensure `dotnet test` covers the new snapshot tests (current CI already runs tests; this change adds coverage)

## Notes
- Versioned fixtures are stored under `tests/nirmata.Aos.Tests/Fixtures/**` (source of truth), following the existing approved-fixture pattern used by `AosInitFixtureTests`.
- The repo-root `.aos/fixtures/**` path is reserved for committed fixture entrypoints (scripts, helper assets). It is not a runtime workspace.
