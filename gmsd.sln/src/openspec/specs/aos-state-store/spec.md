# aos-state-store Specification

## Purpose

Defines canonical AOS workspace contracts and behavioral semantics for $capabilityId.

- **Lives in:** `Gmsd.Aos/*`, `.aos/**`
- **Owns:** Engine-level artifact contracts, validation, and deterministic IO semantics for this capability
- **Does not own:** Plane/orchestrator workflows (owned by `agents-*` capabilities)
## Requirements
### Requirement: State snapshot artifact exists
The system SHALL persist a state snapshot at `.aos/state/state.json`.

The state snapshot JSON MUST be written using the canonical deterministic JSON writer defined by `aos-deterministic-json-serialization`.

The state snapshot MUST include at least:
- `schemaVersion` (integer; currently `1`)
- `cursor` (object) describing the current workflow cursor

The cursor object MAY include cursor v2 fields:
- `cursor.milestoneId` (string | null)
- `cursor.phaseId` (string | null)
- `cursor.taskId` (string | null)
- `cursor.stepId` (string | null)
- `cursor.milestoneStatus` (string | null)
- `cursor.phaseStatus` (string | null)
- `cursor.taskStatus` (string | null)
- `cursor.stepStatus` (string | null)

All status fields are optional; when present and non-null they MUST be one of:
- `not_started`
- `in_progress`
- `blocked`
- `done`

The cursor object MAY also include the legacy cursor reference shape (deprecated for operational cursoring):
- `cursor.kind` (string; recognized artifact kind)
- `cursor.id` (string; artifact id for the given kind)

If either `cursor.kind` or `cursor.id` is present, both MUST be present.

#### Scenario: State snapshot is created as deterministic JSON
- **WHEN** the state store writes a state snapshot
- **THEN** `.aos/state/state.json` exists and is deterministic JSON

#### Scenario: Cursor v2 status values are constrained
- **GIVEN** a state snapshot where `cursor.taskStatus` is set to a value outside the allowed set
- **WHEN** workspace validation is executed
- **THEN** validation fails and reports an invalid cursor status value

#### Scenario: Cursor reference fields are optional
- **GIVEN** a valid state snapshot that includes an empty `cursor` object
- **WHEN** workspace validation is executed
- **THEN** validation succeeds without requiring `cursor.kind` or `cursor.id`

#### Scenario: Cursor kind and id must appear together
- **GIVEN** a state snapshot where `cursor.kind` exists but `cursor.id` is missing (or vice versa)
- **WHEN** workspace validation is executed
- **THEN** validation fails and reports a malformed cursor reference

### Requirement: State snapshot is a deterministic reduction of the event log
The state snapshot at `.aos/state/state.json` MUST be deterministically derivable from `.aos/state/events.ndjson` by applying the ordered event stream (file order) to a baseline snapshot.

The reducer MUST be deterministic:
- it MUST NOT write wall-clock timestamps into `state.json`
- it MUST NOT reorder events (file order is authoritative)
- it MUST write `state.json` using the canonical deterministic JSON writer

#### Scenario: Same event log yields byte-identical snapshots
- **GIVEN** an `events.ndjson` file with a fixed ordered sequence of valid events
- **WHEN** the state store derives `state.json` from that event log twice
- **THEN** the resulting `state.json` bytes are identical

### Requirement: Append-only state event log exists
The system SHALL maintain an append-only event log at `.aos/state/events.ndjson`.

The event log MUST be newline-delimited JSON (NDJSON):
- each non-empty line MUST be a valid JSON object
- new entries MUST be appended to the end of the file
- the file MUST use LF (`\n`) line endings

#### Scenario: Event log appends a new event without rewriting history
- **GIVEN** an existing `.aos/state/events.ndjson` with one or more events
- **WHEN** the state store appends a new event
- **THEN** the new event is appended as a new line and prior lines remain unchanged

### Requirement: Event tail and filter semantics are stable
Consumers MUST be able to read an ordered slice (â€œtailâ€) of `.aos/state/events.ndjson` without re-sorting events.

Tailing MUST preserve file order: returned events MUST appear in the same order as they appear in the file.

The tail/filter surface MUST support:
- `sinceLine` (integer; exclusive): skip all events at or before the given source line number
- `maxItems` (integer; optional): cap the number of returned events while preserving order
- `eventType` (string; optional): include only events whose `eventType` equals the provided value
- `kind` (string; optional, legacy): include only events whose `kind` equals the provided value

If multiple filters are provided (`eventType`, `kind`), an event MUST match all provided filters to be included.

#### Scenario: sinceLine is exclusive and preserves order
- **GIVEN** an `events.ndjson` file with 5 events in file order
- **WHEN** the consumer tails with `sinceLine = 3` and `maxItems = 10`
- **THEN** exactly the events after line 3 are returned, in the same order they appear in the file

#### Scenario: maxItems caps results without changing order
- **GIVEN** an `events.ndjson` file with 10 events in file order
- **WHEN** the consumer tails with `sinceLine = 0` and `maxItems = 3`
- **THEN** exactly the first 3 events are returned, in the same order they appear in the file

### Requirement: Runtime state readiness bootstrap is deterministic
The state layer SHALL provide a deterministic readiness operation to ensure baseline runtime artifacts exist before orchestrator execution.

The readiness operation MUST:
- ensure `.aos/state/state.json` exists and is valid deterministic JSON
- ensure `.aos/state/events.ndjson` exists (empty file permitted)
- perform no writes when artifacts are already valid and present

#### Scenario: Missing state snapshot is created with deterministic baseline content
- **GIVEN** `.aos/state/events.ndjson` exists and is parseable
- **AND** `.aos/state/state.json` is missing
- **WHEN** runtime state readiness bootstrap executes
- **THEN** `state.json` is created at the canonical path
- **AND** `state.json` is deterministic JSON with schema version `1`
- **AND** repeated bootstrap runs with unchanged inputs produce byte-identical snapshot content

#### Scenario: Missing events log is created as an empty append-ready file
- **GIVEN** `.aos/state/state.json` exists and is valid deterministic JSON
- **AND** `.aos/state/events.ndjson` is missing
- **WHEN** runtime state readiness bootstrap executes
- **THEN** `events.ndjson` is created at the canonical path
- **AND** the created log is empty or append-ready NDJSON without invalid placeholder lines

#### Scenario: Readiness bootstrap is idempotent on healthy state
- **GIVEN** `.aos/state/state.json` and `.aos/state/events.ndjson` both exist and are valid
- **WHEN** runtime state readiness bootstrap executes repeatedly
- **THEN** no artifact bytes change across repeated executions

### Requirement: Snapshot can be deterministically re-derived during readiness
When runtime readiness detects a missing or stale `.aos/state/state.json`, the state layer SHALL support deterministic re-derivation from ordered `.aos/state/events.ndjson`.

A re-derived snapshot MUST:
- be produced by replaying events in file order
- be serialized with canonical deterministic JSON rules
- avoid wall-clock or machine-local nondeterministic fields

#### Scenario: Missing snapshot is rebuilt from existing event log
- **GIVEN** `.aos/state/events.ndjson` contains valid ordered events
- **AND** `.aos/state/state.json` is missing
- **WHEN** runtime readiness executes with derive-from-events enabled
- **THEN** `state.json` is created from deterministic event replay
- **AND** repeated derivation from the same event log yields byte-identical `state.json`

#### Scenario: Stale snapshot is deterministically re-derived from events
- **GIVEN** `.aos/state/events.ndjson` contains valid ordered events
- **AND** `.aos/state/state.json` exists but does not match deterministic replay output for the same event stream
- **WHEN** runtime readiness executes with derive-from-events enabled
- **THEN** `state.json` is replaced with the deterministic replay result
- **AND** repeated readiness runs with unchanged events keep `state.json` byte-identical

#### Scenario: Derivation failure returns actionable diagnostics
- **GIVEN** `.aos/state/events.ndjson` contains an invalid non-empty line
- **WHEN** runtime readiness attempts to derive `state.json`
- **THEN** readiness fails with a diagnostic that identifies the invalid event source
- **AND** state artifacts are not partially overwritten

