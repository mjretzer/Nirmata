## ADDED Requirements
### Requirement: Fix planner emits canonical schema-valid plan artifacts
The fix planner MUST emit `plan.json` using the canonical task-plan/fix-plan schema definitions and typed contract models shared with other workflow consumers.

Each emitted fix `plan.json` MUST:
- include `schemaVersion`
- represent `fileScopes` with canonical object entries using `path`
- represent acceptance/verification structures using canonical contract fields
- validate against the registered schema before persistence

#### Scenario: Fix planner writes canonical `fileScopes` entries
- **GIVEN** fix planning produces a set of affected files
- **WHEN** it writes `.aos/spec/tasks/<task-id>/plan.json`
- **THEN** each file scope entry is written as an object containing canonical `path` and the artifact validates against schema
