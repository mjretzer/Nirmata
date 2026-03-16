# Change: Add deterministic state preflight bootstrap

## Why
State-dependent workflows can fail when `.aos/state/state.json` or `.aos/state/events.ndjson` are missing or inconsistent at runtime, which surfaces as brittle failures like “Snapshot not set” instead of recoverable agent behavior.

## What Changes
- Add a deterministic startup hook (`EnsureWorkspaceInitialized`) for runtime state readiness.
- Require preflight to ensure baseline state artifacts exist before phase dispatch.
- Allow deterministic snapshot derivation from events when snapshot is missing or detected stale.
- Require conversational recovery output when preflight cannot repair state readiness.

## Impact
- Affected specs:
  - `aos-state-store`
  - `agents-orchestrator-workflow`
  - `prerequisite-validation`
- Affected code (expected):
  - `nirmata.Aos` state/workspace initialization surfaces and store behavior
  - `nirmata.Agents` preflight/prerequisite validation and orchestrator control flow
  - associated tests in `tests/nirmata.Aos.Tests` and `tests/nirmata.Agents.Tests`
