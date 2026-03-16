# Spec Delta: Structured Phase Planning

## ADDED Requirements

### Requirement: Phase Planning Output Schema
The `PhasePlanner` MUST produce a JSON object following a strict schema that includes a list of tasks, their file scopes, and verification steps.

#### Scenario: Valid Phase Plan Generation
- **Given** a phase brief and context
- **When** the `PhasePlanner` is invoked
- **Then** it MUST return a JSON object containing:
    - `tasks`: An array of task objects
    - Each task MUST have `id`, `title`, `description`
    - Each task MUST have `fileScopes`: An array of file paths or patterns
    - Each task MUST have `verificationSteps`: An array of strings describing how to verify the task

### Requirement: Schema Validation on Ingest
`PhasePlannerHandler` MUST validate the LLM-generated JSON against the `PhasePlan` schema before processing or persisting the plan.

#### Scenario: Invalid Phase Plan Rejection
- **Given** an LLM response that does not match the `PhasePlan` schema
- **When** `PhasePlannerHandler` receives the response
- **Then** it MUST record a validation failure and either retry or return a failure result.
