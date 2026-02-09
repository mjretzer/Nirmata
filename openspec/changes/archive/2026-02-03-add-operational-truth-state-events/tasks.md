## 1. Spec + contracts
- [x] 1.1 Update `aos-state-store` spec to define cursor v2, reducer determinism, and tail/filter semantics.
- [x] 1.2 Update `aos-workspace-validation` spec to require object-per-line and schema validation for `events.ndjson`.
- [x] 1.3 Update `aos-public-api-surface` spec to define a usable public `IStateStore` and stable state contract DTOs.

## 2. Cursor + reducer implementation
- [x] 2.1 Introduce stable contract DTOs under `Gmsd.Aos/Contracts/State/**` for state snapshot + event entries and filter options.
- [x] 2.2 Implement a deterministic reducer that derives `state.json` from `events.ndjson` for the supported event set.
- [x] 2.3 Update `state-snapshot.schema.json` to reflect the cursor v2 fields and allowed status values.

## 3. Event tailing + validation
- [x] 3.1 Add a state-store API for tailing events with filters (`sinceLine`, `maxItems`, `eventType` and legacy `kind`).
- [x] 3.2 Strengthen `AosWorkspaceValidator` state-layer validation: each non-empty NDJSON line must be a JSON object and validate against `event.schema.json`.

## 4. Public surface + integration
- [x] 4.1 Flesh out `Gmsd.Aos.Public` state store interface so consumers can read snapshots, append events, and tail/filter events.
- [x] 4.2 Ensure internal engine types remain internal (public APIs exchange only `Gmsd.Aos.Contracts.*` types).

## 5. Tests
- [x] 5.1 Add determinism tests: same event log yields byte-identical `state.json`.
- [x] 5.2 Add tail/filter tests: stable ordering, correct filtering, correct paging (`sinceLine` + `maxItems`).
- [x] 5.3 Add workspace validation tests for invalid NDJSON: non-object lines, schemaVersion mismatch, malformed JSON.

