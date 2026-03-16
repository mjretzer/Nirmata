## Context
The AOS workspace separates truth layers:
- `.aos/spec/*` intended truth
- `.aos/state/*` operational truth (cursor + events)
- `.aos/evidence/*` provable truth

This change makes the state layer operationally useful by:
- defining a concrete cursor model
- defining deterministic derivation from events
- defining a stable event tail/filter contract

## Goals / Non-Goals
### Goals
- Make `.aos/state/state.json` a **deterministically derived** snapshot from `.aos/state/events.ndjson`.
- Define a cursor model that captures progress at: milestone → phase → task → step.
- Provide stable tail/filter semantics that preserve on-disk ordering and avoid re-sorting.
- Keep event schema permissive (per the chosen scope): require `schemaVersion` only, but still enforce object-per-line and schema validation.

### Non-Goals
- Introducing a multi-project cursor model (single-project invariants remain).
- Enforcing timestamps or strong event typing via schema (left for a later change).
- Defining a full workflow state machine beyond “progress cursor + statuses”.

## Cursor model (v2)
`state.json` is the latest derived snapshot. It MUST be written via the deterministic JSON writer.

### Snapshot shape (conceptual)
- `schemaVersion: 1`
- `cursor` (object)
  - `milestoneId` (string | null)
  - `phaseId` (string | null)
  - `taskId` (string | null)
  - `stepId` (string | null)
  - `milestoneStatus` (string | null)
  - `phaseStatus` (string | null)
  - `taskStatus` (string | null)
  - `stepStatus` (string | null)

### Status values
Statuses are stable strings with ordinal comparisons and no localization:
- `not_started`
- `in_progress`
- `blocked`
- `done`

All status fields are optional; when present they MUST be one of the allowed values.

### Compatibility with existing cursor reference invariants
Earlier milestones introduced `cursor.kind` + `cursor.id` invariants. This change treats that reference form as deprecated for operational cursoring (it may remain tolerated temporarily), and defines the primary cursoring fields as `milestoneId/phaseId/taskId/stepId`.

## Event model (v1 schema, conventions)
The event schema remains permissive (only `schemaVersion: 1` required) but engine-emitted events SHOULD follow a stable convention:
- `schemaVersion: 1`
- `eventType: string` (preferred)
- `timestampUtc: string` (optional; not used in determinism-sensitive snapshot derivation)
- `data: object` (optional)

### Compatibility with legacy event emitters
Some existing engine code emits checkpoint events using `kind` rather than `eventType`. Tail/filter SHOULD support both fields for filtering, and reducers MAY interpret either field.

## State reducer
The reducer is a pure function:
- Inputs:
  - baseline seed snapshot (empty cursor object, schemaVersion=1) OR existing `state.json`
  - ordered event stream from `events.ndjson` (file order)
- Output:
  - next snapshot written canonically to `state.json`

### Determinism rules
- Do not write wall-clock timestamps into `state.json`.
- Do not reorder events (file order is authoritative).
- Always write with the canonical deterministic JSON writer (atomic + no-churn).
- Any derived collections MUST be deterministically ordered (ordinal sort) if introduced later.

## Tail + filters
Tail is defined as reading an ordered slice without re-sorting.

### Proposed filter surface
- `eventType` filter (matches `eventType` string when present)
- `kind` filter (legacy compatibility)
- `sinceLine` (exclusive line number cursor)
- `maxItems` (cap results; preserves order)

Tailing MUST return events in the same order they appear in the file.

## Validation
Workspace validation for the state layer MUST enforce:
- `events.ndjson` exists
- each non-empty line is valid JSON AND a JSON object
- each non-empty line validates against `nirmata:aos:schema:event:v1`

