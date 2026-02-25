# Change: Add versioned artifact JSON contracts and typed models

## Why
Multiple engine components currently interpret `plan.json` and related fields (such as `fileScopes`) with incompatible shapes. This creates non-deterministic behavior across planning, execution, verification, and UI rendering.

## What Changes
- Define canonical, versioned JSON contracts for task planning artifacts with one schema per artifact type.
- Establish schema versioning policy and compatibility expectations for producers/consumers.
- Define generated typed model expectations so all readers/writers share the same contract model.
- Require all artifact writers to emit schema-valid deterministic JSON.
- Require all artifact readers to validate on read and emit friendly diagnostic artifacts on validation failure.
- Standardize `fileScopes` representation and path field naming for task plans.

## Implementation Scope
- Schema and registry alignment:
  - Add/update schema pack entries for `gmsd.task-plan` and `gmsd.command-proposal`.
  - Add/update registry metadata for current version, supported versions, and deprecation state.
- Shared model alignment:
  - Align planner, executor, verifier, and UI adapter models to one canonical contract shape.
  - Remove or adapt divergent local DTO variants that permit shape drift.
- Writer consistency:
  - Enforce deterministic serializer settings and canonical property naming/order.
  - Ensure all updated writers emit the current schema version only.
- Reader enforcement and diagnostics:
  - Validate artifacts at read boundaries before business logic execution.
  - Emit structured diagnostics with schema identity, version, path, and remediation guidance.
  - Fail fast for out-of-contract artifacts and block downstream execution.

## Rollout and Compatibility
- Initial rollout sets `schemaVersion: 1` as current for the canonical contracts introduced/standardized by this change.
- Readers MAY accept only explicitly listed supported versions per schema `$id`.
- Deprecated versions (if any) remain explicitly supported only during bounded transition windows and must emit warning diagnostics.
- Removal of deprecated versions requires a follow-up explicit change proposal.

## Acceptance Outcomes
- A single schema registry lookup resolves each artifact type to one canonical schema `$id` and current version.
- Task-plan and command-proposal writers produce deterministic, schema-valid JSON payloads.
- Planner/executor/fix-planner read paths reject unsupported or malformed artifacts before business logic executes.
- Validation failures produce actionable diagnostic artifacts consumable by both operators and UI.
- Test coverage demonstrates valid and invalid behaviors across writer/reader boundaries.

## Impact
- Affected specs:
  - `aos-schema-registry`
  - `aos-workspace-validation`
  - `phase-planning`
  - `agents-task-executor`
  - `agents-fix-planner`
  - `command-proposal`
- Affected code (expected during apply stage):
  - `Gmsd.Aos` schema registry + validator/reporting components
  - `Gmsd.Agents` planning/execution/fix-planning models and writers/readers
  - UI-facing event/contract adapters that consume task-plan artifacts
