# backlog-triage Specification

## Purpose

Defines the durable contract for $capabilityId in the nirmata platform.

- **Lives in:** See repo projects and `.aos/**` artifacts as applicable
- **Owns:** Capability-level contract and scenarios
- **Does not own:** Unrelated domain concerns outside this capability
## Requirements
### Requirement: DeferredIssuesCurator triages issues
The system SHALL provide an `IDeferredIssuesCurator` interface that triages issues from `.aos/spec/issues/` and routes urgent items into the main execution loop.

The implementation MUST:
- Scan `.aos/spec/issues/` for `ISS-*.json` files
- Assess each issue's severity (urgent, high, medium, low)
- Make a routing recommendation (main-loop, deferred, or discard)
- Update the `ISS-*.json` with triage decision (routingDecision, triagedAt, triagedBy)
- Append triage events to `.aos/state/events.ndjson`
- Return a triage report listing issues and their routing decisions

#### Scenario: Urgent issue routes to main loop
- **GIVEN** an `ISS-0001.json` with severity "urgent" describing a critical bug
- **WHEN** the DeferredIssuesCurator is invoked
- **THEN** the triage report recommends routing to main loop
- **AND** the `ISS-0001.json` is updated with `routingDecision: "main-loop"`
- **AND** a triage event is appended to `.aos/state/events.ndjson`

#### Scenario: Non-urgent issue remains deferred
- **GIVEN** an `ISS-0002.json` with severity "low" describing a nice-to-have feature
- **WHEN** the DeferredIssuesCurator is invoked
- **THEN** the triage report recommends deferring the issue
- **AND** the `ISS-0002.json` is updated with `routingDecision: "deferred"`

### Requirement: TodoCapturer captures TODOs without affecting cursor
The system SHALL provide an `ITodoCapturer` interface that captures TODO items to `.aos/context/todos/` without affecting the current execution cursor.

The implementation MUST:
- Accept TODO input (description, source context, optional priority)
- Generate a unique TODO ID (TODO-XXXX format)
- Write `TODO-*.json` to `.aos/context/todos/` with fields:
  - id, description, source (run/task reference), capturedAt, priority, status ("active")
- Append capture events to `.aos/state/events.ndjson`
- NOT modify `.aos/state/cursor.json` or any other state file
- Return the generated TODO ID

#### Scenario: TODO is captured during execution
- **GIVEN** a running execution with cursor at phase PH-001
- **WHEN** the TodoCapturer is invoked with description "Refactor helper method"
- **THEN** a `TODO-0001.json` file is created in `.aos/context/todos/`
- **AND** the cursor file remains unchanged (still at PH-001)
- **AND** a capture event is appended to `.aos/state/events.ndjson`

#### Scenario: Multiple TODOs captured independently
- **GIVEN** two separate TODO capture invocations
- **WHEN** both are processed
- **THEN** two distinct `TODO-*.json` files exist with unique IDs
- **AND** neither capture affects the execution cursor

### Requirement: TodoReviewer reviews and promotes TODOs
The system SHALL provide an `ITodoReviewer` interface that reviews captured TODOs and promotes them to tasks or roadmap phases.

The implementation MUST:
- List all active TODOs from `.aos/context/todos/`
- Support selecting a TODO by ID for review
- Provide promotion paths:
  - "task": Create task spec under `.aos/spec/tasks/{task-id}/` with TODO as source
  - "phase": Insert phase into roadmap referencing the TODO
  - "discard": Mark TODO as "discarded" (do not delete, preserve history)
- Update `TODO-*.json` status on promotion ("promoted-to-task", "promoted-to-phase", "discarded")
- Write review events to `.aos/state/events.ndjson`
- Return the promotion result with references to created artifacts

#### Scenario: TODO promoted to task
- **GIVEN** a `TODO-0001.json` with description "Implement validation"
- **WHEN** the TodoReviewer promotes it to task
- **THEN** a new task directory `.aos/spec/tasks/TSK-0001/` is created
- **AND** `task.json` is generated with TODO reference as source
- **AND** `TODO-0001.json` status is updated to "promoted-to-task"

#### Scenario: TODO promoted to roadmap phase
- **GIVEN** a `TODO-0002.json` with description "Add caching layer"
- **WHEN** the TodoReviewer promotes it to roadmap phase
- **THEN** a new phase is inserted into `.aos/spec/roadmap.json`
- **AND** phase references the TODO ID in its source metadata
- **AND** `TODO-0002.json` status is updated to "promoted-to-phase"

#### Scenario: TODO discarded
- **GIVEN** a `TODO-0003.json` that is no longer relevant
- **WHEN** the TodoReviewer marks it as discarded
- **THEN** `TODO-0003.json` status is updated to "discarded"
- **AND** the TODO remains in `.aos/context/todos/` for history

### Requirement: Backlog handlers integrate with orchestrator
The system SHALL provide handler implementations that integrate the backlog workflows with the orchestrator's gating and dispatch system.

The implementation MUST:
- Provide `DeferredIssuesCuratorHandler` implementing the handler pattern
- Provide `TodoCapturerHandler` implementing the handler pattern
- Provide `TodoReviewerHandler` implementing the handler pattern
- Support command-based invocation from orchestrator
- Return handler results compatible with orchestrator expectations
- Integrate with `IRunLifecycleManager` for evidence capture

#### Scenario: Curator handler executes triage
- **GIVEN** a command to consider issues
- **WHEN** the `DeferredIssuesCuratorHandler` is invoked by the orchestrator
- **THEN** it executes the triage workflow and returns success/failure result
- **AND** triage evidence is captured to the current run's artifacts

#### Scenario: Capturer handler executes capture
- **GIVEN** a command to add a TODO during execution
- **WHEN** the `TodoCapturerHandler` is invoked by the orchestrator
- **THEN** it captures the TODO without affecting cursor position
- **AND** returns success with the generated TODO ID

