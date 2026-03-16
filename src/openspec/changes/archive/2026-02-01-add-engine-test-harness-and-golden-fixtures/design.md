## Context
The repository already uses an “approved fixture” pattern for determinism proof (e.g., `tests/nirmata.Aos.Tests/AosInitFixtureTests.cs` compares a produced `.aos/*` tree against `tests/nirmata.Aos.Tests/Fixtures/Approved/.aos` with normalization/guardrails). The roadmap milestone `add-engine-test-harness-and-golden-fixtures` expands that approach into a broader engine regression harness (JSON, routing, schema validation) with a canonical entrypoint script.

## Goals / Non-Goals
### Goals
- Provide a **versioned** golden-fixture corpus for AOS engine regression coverage.
- Ensure snapshot tests fail on any nondeterministic drift across machines.
- Make `.aos/fixtures/scripts/engine-mvp.ps1` the canonical regression entrypoint.
- Preserve backwards compatibility for existing workflows by keeping a wrapper at `scripts/engine-mvp.ps1`.

### Non-Goals
- Changing engine runtime behavior or CLI semantics in this proposal stage.
- Introducing new external tooling dependencies for snapshot testing (prefer existing xUnit patterns).

## Decisions
### Decision: Versioned fixture corpus lives under `tests/nirmata.Aos.Tests/Fixtures/**`
Fixtures are treated as test assets and kept alongside the AOS engine test project, matching the current pattern:
- `tests/nirmata.Aos.Tests/Fixtures/Approved/**` is the “golden” corpus
- tests run the CLI/engine and compare produced artifacts/outputs to approved fixtures

### Decision: Canonical regression script moves to `.aos/fixtures/scripts/engine-mvp.ps1`
To align with the roadmap’s “canonical regression entrypoint”, we will:
- Move `scripts/engine-mvp.ps1` → `.aos/fixtures/scripts/engine-mvp.ps1`
- Keep `scripts/engine-mvp.ps1` as a small wrapper that invokes the canonical script (so existing instructions/links don’t break)

### Decision: `.gitignore` should ignore runtime `.aos/**` but allow committed `.aos/fixtures/**`
The engine produces `.aos/**` workspace artifacts that should not be committed. However, this change introduces a committed fixture-entrypoint location under `.aos/fixtures/**`.

Planned rule shape (to be implemented in apply stage):
- Ignore: `.aos/**`
- Unignore: `!.aos/fixtures/**`

## Risks / Trade-offs
- Putting any committed content under `.aos/` can be confusing (workspace vs fixtures). Mitigation: document intent in proposal/tasks and enforce `.gitignore` rules so only `.aos/fixtures/**` is tracked.

## Migration Plan
- Preserve compatibility by keeping a wrapper script at the old path (`scripts/engine-mvp.ps1`) that calls the new canonical location.
