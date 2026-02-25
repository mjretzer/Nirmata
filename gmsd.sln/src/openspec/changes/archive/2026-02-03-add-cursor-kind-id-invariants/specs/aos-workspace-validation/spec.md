## ADDED Requirements

### Requirement: Workspace validation enforces cursor reference invariants
When `.aos/state/state.json` includes a cursor reference (`cursor.kind` + `cursor.id`), `aos validate workspace` MUST validate that the cursor reference is deterministic and resolvable.

Workspace validation MUST fail if:
- `cursor.kind` is present without `cursor.id` (or vice versa)
- `cursor.kind` is not a recognized artifact kind
- `cursor.id` cannot be parsed as an id for the given kind, or is not canonical for that kind
- the referenced artifact does not exist at the canonical contract path for the kind/id

If a catalog index exists for the referenced kind, workspace validation MUST also fail if the cursor id is not present in that catalog index.

#### Scenario: Cursor kind is required when cursor id is present
- **GIVEN** `.aos/state/state.json` contains `cursor.id` but omits `cursor.kind`
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails and reports a malformed cursor reference

#### Scenario: Cursor rejects an unrecognized kind deterministically
- **GIVEN** `.aos/state/state.json` contains `cursor.kind = "unknown-kind"` and `cursor.id = "X-0001"`
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails and reports that the cursor kind is not recognized (including the list of expected kinds)

#### Scenario: Cursor reference to a missing artifact fails deterministically
- **GIVEN** `.aos/state/state.json` contains a cursor reference `{ kind: "milestone", id: "MS-0001" }`
- **AND** the referenced milestone does not exist at its canonical contract path
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails and reports the missing referenced artifact contract path

