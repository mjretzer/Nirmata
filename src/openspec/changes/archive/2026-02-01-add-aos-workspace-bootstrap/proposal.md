# Change: Add AOS workspace bootstrap via `aos init`

## Why
Milestone E1 depends on a deterministic, machine-independent `.aos/*` workspace contract so that later commands (schema validation, run lifecycle, execution) have a stable place to read/write artifacts.

## What Changes
- Add a CLI command `aos init` (implemented in `nirmata.Aos`) that creates a canonical `.aos/` skeleton (folders + baseline index files).
- Seed `.aos/spec/project.json` with a minimal, valid JSON skeleton (no machine-specific values).
- Stub minimal schema registry wiring by creating `.aos/schemas/*` placeholder artifacts that later validation commands can consume.
- Define idempotent behavior and clear failure modes when an existing `.aos/` is non-compliant.

## Impact
- **Affected specs**: new capability `aos-workspace-bootstrap`
- **Affected code**: engine workspace contracts and CLI command handler in `nirmata.Aos`, plus a fixture-based test that proves determinism across machines

