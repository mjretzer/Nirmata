## ADDED Requirements
### Requirement: Phase planner emits canonical task plan contract
The `PhasePlanner` MUST emit `plan.json` using the canonical task-plan schema and the canonical typed model.

The emitted plan MUST:
- include `schemaVersion`
- encode `fileScopes` as an array of objects
- use `path` as the canonical file scope path field
- conform to the registered task-plan schema before persistence

#### Scenario: Phase planner writes schema-valid canonical plan
- **GIVEN** a phase decomposition result is ready to persist
- **WHEN** the phase planner writes `.aos/spec/tasks/<task-id>/plan.json`
- **THEN** the file is deterministic JSON that validates against the canonical task-plan schema with canonical `fileScopes[].path`

### Requirement: Phase planning rejects invalid plan artifacts before persistence
The phase planning workflow MUST validate candidate task-plan JSON against the canonical schema before writing to disk.

#### Scenario: Invalid file scope shape is rejected during phase planning
- **GIVEN** a candidate task plan where `fileScopes` contains string entries instead of objects
- **WHEN** phase planning validation executes
- **THEN** planning fails with a validation diagnostic and does not persist the invalid plan artifact
