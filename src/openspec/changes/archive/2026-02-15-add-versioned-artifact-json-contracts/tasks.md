## 1. Specification and contract design
- [x] 1.1 Define canonical artifact contract inventory (task `plan.json`, fix planning outputs, command proposals) and assign one schema per artifact type.
  - [x] 1.1.1 Confirm each artifact maps to exactly one schema `$id` in the inventory table.
  - [x] 1.1.2 Confirm task-planner and fix-planner `plan.json` share one canonical task-plan contract.
  - [x] 1.1.3 Document when a future artifact requires a new schema `$id` instead of reusing an existing one.
- [x] 1.2 Define schema versioning policy and compatibility rules (current version, supported versions, and deprecation behavior).
  - [x] 1.2.1 Define writer policy: emit only `current schemaVersion` for each schema `$id`.
  - [x] 1.2.2 Define reader policy: reject unsupported versions with deterministic diagnostic content.
  - [x] 1.2.3 Define deprecation lifecycle: supported+deprecated -> warning -> explicit removal in later change.
- [x] 1.3 Define canonical `fileScopes` shape and path naming for task-plan artifacts.
  - [x] 1.3.1 Define `fileScopes` as object entries and prohibit string entries.
  - [x] 1.3.2 Define `fileScopes[].path` as the only canonical path property name.
  - [x] 1.3.3 Define alternate names (`filePath`, `relativePath`) as out-of-contract.

## 2. Engine schema + model alignment
- [x] 2.1 Add/update schema pack entries and registry metadata for each affected artifact type.
  - [x] 2.1.1 Add/update schema documents for `nirmata.task-plan` and `nirmata.command-proposal` in the schema pack.
  - [x] 2.1.2 Add/update registry metadata for current version, supported versions, and deprecation flags.
  - [x] 2.1.3 Add startup validation that fails when required schema files or metadata entries are missing.
- [x] 2.2 Generate or align typed models from canonical contracts so planner, executor, verifier, and UI adapters share one shape.
  - [x] 2.2.1 Align task-plan models to canonical `fileScopes` object shape with `path` property.
  - [x] 2.2.2 Align command-proposal models to canonical schema property names/types.
  - [x] 2.2.3 Remove or adapt divergent local DTOs/adapters so all read/write boundaries use canonical models.
- [x] 2.3 Enforce deterministic canonical JSON writing for all updated artifact writers.
  - [x] 2.3.1 Centralize serializer options (property naming, enum handling, null policy).
  - [x] 2.3.2 Enforce deterministic property ordering for artifact output.
  - [x] 2.3.3 Add snapshot coverage proving repeated writes produce stable JSON output.

## 3. Validation and diagnostics enforcement
- [x] 3.1 Enforce schema validation at all artifact read boundaries before business logic runs.
  - [x] 3.1.1 Enumerate read boundaries for planner, executor, fix planner, verifier, and adapters.
  - [x] 3.1.2 Insert schema validation immediately after parse/deserialization at each boundary.
  - [x] 3.1.3 Block downstream business logic when validation returns invalid.
- [x] 3.2 Emit friendly diagnostic artifacts when validation fails, including contract path, schema ID, and actionable message.
  - [x] 3.2.1 Define diagnostic artifact schema/shape for validation failures.
  - [x] 3.2.2 Include required fields: artifact path, schema `$id`, declared version, supported versions, and error list.
  - [x] 3.2.3 Write diagnostics to stable runtime locations and ensure message text is human-actionable.
- [x] 3.3 Ensure out-of-contract artifacts fail fast and do not execute downstream workflows.
  - [x] 3.3.1 Return terminal failure status at validation gate for out-of-contract payloads.
  - [x] 3.3.2 Prevent scheduling/execution side effects after validation failure.
  - [x] 3.3.3 Surface failure status to callers/UI with correlation to emitted diagnostics.

## 4. Verification
- [x] 4.1 Add/update unit and integration tests for valid/invalid contract read/write behavior across planner, executor, and fix planner.
  - [x] 4.1.1 Add unit tests for valid payloads for each schema `$id` and supported version.
  - [x] 4.1.2 Add unit tests for invalid shape, unsupported version, and malformed `fileScopes` payloads.
  - [x] 4.1.3 Add integration tests confirming invalid artifacts halt workflow and emit diagnostics.
- [x] 4.2 Run `openspec validate add-versioned-artifact-json-contracts --strict` and resolve all findings.
  - [x] 4.2.1 Validate after task/status updates and fix any delta formatting issues.
  - [x] 4.2.2 Re-run strict validation after spec/task/proposal edits until no findings remain.
- [x] 4.3 Run targeted solution tests that exercise schema-valid writer and reader behavior for task plans and command proposals.
  - [x] 4.3.1 Run targeted tests for task-plan writer/reader canonical shape + deterministic serialization.
  - [x] 4.3.2 Run targeted tests for command-proposal writer/reader schema validation.
  - [x] 4.3.3 Record test command output in change notes/PR context for reviewer verification (`verification-notes.md`).
