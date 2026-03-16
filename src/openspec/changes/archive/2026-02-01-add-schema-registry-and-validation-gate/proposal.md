# Change: Add schema registry and validation gate

## Why
The engine needs a deterministic, versioned schema registry plus a validation gate so malformed or non-compliant `.aos/*` artifacts are detected **before** any workflow proceeds.

This milestone builds on `aos-workspace-bootstrap` by adding first-class validation commands and enforcing invariants for the “single-project workspace” model.

## What Changes
- Add a **JSON Schema registry loader** in `nirmata.Aos` that loads schemas shipped with the engine (embedded resources) so validation is consistent and versioned with the library.
- Add an **invariant validator** (fails fast) for cross-file rules (not expressible via JSON Schema alone).
- Add **canonical schema filename enforcement** for shipped schema assets (e.g., accept `context-pack.schema.json`, reject `context.pack.schema.json`).
- Add CLI commands:
  - `aos validate schemas`
  - `aos validate workspace` (defaults to all layers)
  - Optional: `aos validate workspace --layers spec,state,evidence,codebase,context`
- Optional: one-time migration helper for renamed schema files (only if/when on-disk schema files exist and a safe deterministic rename can be proven).

## Invariants (initial set)
- Fail if `.aos/spec/project.json` is missing
- Fail if `.aos/spec/projects.json` exists
- Fail if `.aos/state/active-project.json` exists
- Fail if `.aos/spec/roadmap.json` references any project other than the single `project.json`

## Impact
- **Affected specs**:
  - new capability `aos-schema-registry`
  - new capability `aos-workspace-validation`
- **Affected code**:
  - `nirmata.Aos` CLI command routing and new validation components
  - new embedded schema assets shipped with `nirmata.Aos`
  - tests proving schema validation and invariant enforcement

