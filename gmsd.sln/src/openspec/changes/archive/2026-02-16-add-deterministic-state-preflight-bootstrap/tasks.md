## 1. Specification
- [x] 1.1 Update `specs/aos-state-store/spec.md` delta with explicit state bootstrap rules.
- [x] 1.1.1 Add/modify a requirement that SHALL run deterministic workspace state readiness bootstrap before runtime state usage.
- [x] 1.1.2 Add scenario: missing `.aos/state/state.json` is created with deterministic baseline snapshot content.
- [x] 1.1.3 Add scenario: missing `.aos/state/events.ndjson` is created as an empty event log during bootstrap.
- [x] 1.1.4 Add scenario: stale or missing snapshot is deterministically re-derived from events.
- [x] 1.2 Update `specs/agents-orchestrator-workflow/spec.md` delta with preflight ordering requirements.
- [x] 1.2.1 Add/modify a requirement that SHALL invoke `EnsureWorkspaceInitialized()` before any write-phase dispatch.
- [x] 1.2.2 Add scenario: initialization succeeds and write-phase dispatch continues.
- [x] 1.2.3 Add scenario: initialization fails and write-phase dispatch is blocked.
- [x] 1.3 Update `specs/prerequisite-validation/spec.md` delta with conversational recovery behavior.
- [x] 1.3.1 Add/modify a requirement that SHALL return structured prerequisite/preflight diagnostics when repair fails.
- [x] 1.3.2 Add scenario defining required diagnostic fields (failure code, failing prerequisite, attempted repairs, suggested fixes).
- [x] 1.3.3 Add scenario: user receives actionable next steps instead of an unstructured runtime error.

## 2. Implementation
- [x] 2.1 Wire preflight initialization into orchestrator flow.
- [x] 2.1.1 Locate write-phase entrypoint used by orchestrator dispatch.
- [x] 2.1.2 Insert `EnsureWorkspaceInitialized()` call before write-phase dispatch execution.
- [x] 2.1.3 Ensure failures short-circuit dispatch and return preflight failure result.
- [x] 2.2 Implement deterministic snapshot bootstrap behavior.
- [x] 2.2.1 On missing `.aos/state/state.json`, create file and seed deterministic baseline snapshot data.
- [x] 2.2.2 Ensure baseline snapshot data is stable across runs for identical input state.
- [x] 2.3 Implement events log bootstrap behavior.
- [x] 2.3.1 On missing `.aos/state/events.ndjson`, create file during preflight.
- [x] 2.3.2 Ensure created log is parseable by existing event replay logic.
- [x] 2.4 Implement deterministic derive-from-events path.
- [x] 2.4.1 Detect missing snapshot and stale snapshot conditions before state use.
- [x] 2.4.2 Rebuild snapshot from `events.ndjson` using existing deterministic ordering rules.
- [x] 2.4.3 Persist rebuilt snapshot to `.aos/state/state.json`.
- [x] 2.5 Implement structured preflight failure diagnostics.
- [x] 2.5.1 Define/extend response contract to include: prerequisite name, failure reason, repair attempts, and suggested fix actions.
- [x] 2.5.2 Map internal initialization failures to this structured contract.
- [x] 2.5.3 Ensure conversational output uses structured diagnostics instead of generic "Snapshot not set"-style errors.

## 3. Validation
- [x] 3.1 Add/update test: missing `state.json` triggers deterministic snapshot bootstrap.
- [x] 3.1.1 Assert file is created.
- [x] 3.1.2 Assert seeded snapshot content is deterministic for same input.
- [x] 3.2 Add/update test: missing `events.ndjson` is created during preflight.
- [x] 3.2.1 Assert file exists after initialization.
- [x] 3.2.2 Assert log format is compatible with replay parser.
- [x] 3.3 Add/update test: missing or stale snapshot is re-derived from events.
- [x] 3.3.1 Assert rebuild path is executed under missing/stale conditions.
- [x] 3.3.2 Assert resulting snapshot matches deterministic replay output.
- [x] 3.4 Add/update test: unrecoverable initialization returns structured conversational diagnostics.
- [x] 3.4.1 Assert response includes failure code, failing prerequisite, attempted repairs, and suggested fixes.
- [x] 3.4.2 Assert generic low-context errors are not surfaced to the user.
- [x] 3.5 Run `openspec validate add-deterministic-state-preflight-bootstrap --strict` and record passing output.
- [x] 3.6 Run targeted test suites for touched areas and record passing output in verification notes.
