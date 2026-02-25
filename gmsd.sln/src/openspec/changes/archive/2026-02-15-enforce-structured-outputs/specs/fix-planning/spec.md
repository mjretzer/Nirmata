# Spec Delta: Structured Fix Planning

## ADDED Requirements

### Requirement: Fix Planning Output Schema
The `FixPlanner` MUST produce a JSON object following a strict schema that maps identified issues to specific proposed changes and verification tests.

#### Scenario: Valid Fix Plan Generation
- **Given** a set of issue IDs and codebase context
- **When** the `FixPlanner` is invoked
- **Then** it MUST return a JSON object containing:
    - `fixes`: An array of fix objects
    - Each fix MUST have `issueId`, `description`
    - Each fix MUST have `proposedChanges`: An array of objects with `file` and `changeDescription`
    - Each fix MUST have `tests`: An array of strings describing new or updated tests to run

### Requirement: Schema Validation on Ingest
`FixPlannerHandler` MUST validate the LLM-generated JSON against the `FixPlan` schema before generating fix task artifacts.

#### Scenario: Invalid Fix Plan Rejection
- **Given** an LLM response that does not match the `FixPlan` schema
- **When** `FixPlannerHandler` receives the response
- **Then** it MUST record a validation failure and return a failure result.
