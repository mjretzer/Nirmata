## ADDED Requirements

### Requirement: Gate Selection

The system SHALL emit a `gate.selected` event when the gating engine selects a target phase for a workflow operation. The event MUST include the target phase identifier, reasoning for the selection, the proposed action details, and a flag indicating whether user confirmation is required.

**Event Structure**:
```json
{
  "id": "evt-uuid-002",
  "type": "gate.selected",
  "timestamp": "2026-02-10T12:00:01Z",
  "correlationId": "corr-uuid-123",
  "payload": {
    "targetPhase": "Planner",
    "reasoning": "Input matches planning intent for foundation phase. Project spec exists but no phase plan found at cursor position PH-0001.",
    "requiresConfirmation": true,
    "proposedAction": {
      "type": "CreatePhasePlan",
      "description": "Generate task plan for foundation phase including file scope and verification criteria",
      "phaseId": "PH-0001",
      "scope": [".aos/spec/roadmap.json", ".aos/spec/phases/PH-0001/"],
      "estimatedImpact": "Will create task specs and plan files in .aos/spec/phases/PH-0001/"
    }
  }
}
```

#### Scenario: Gate selected with confirmation required

- **GIVEN** a write workflow intent (e.g., planning, execution)
- **WHEN** gating engine selects target phase
- **THEN** `gate.selected` event is emitted
- **AND** `requiresConfirmation` is `true`
- **AND** `proposedAction` contains complete action description
- **AND** `reasoning` explains the selection context

#### Scenario: Gate selected without confirmation

- **GIVEN** a read-only or low-risk workflow intent (e.g., status check, verification)
- **WHEN** gating engine selects target phase
- **THEN** `gate.selected` event is emitted
- **AND** `requiresConfirmation` is `false`
- **AND** event still includes full reasoning for transparency

#### Scenario: User confirmation response

- **GIVEN** a `gate.selected` event with `requiresConfirmation: true`
- **WHEN** user confirms the proposed action
- **THEN** orchestrator proceeds with `run.started` and phase execution
- **AND** confirmation is recorded in run evidence

#### Scenario: User rejection response

- **GIVEN** a `gate.selected` event with `requiresConfirmation: true`
- **WHEN** user rejects the proposed action
- **THEN** orchestrator emits `gate.rejected` event
- **AND** no `run.started` event is emitted
- **AND** chat responder provides alternative suggestions

### Requirement: Event Sequencing with Confirmation Gate

Events MUST be emitted in a logical sequence that reflects the orchestration flow, with confirmation gate inserted before write operations.

#### Scenario: Workflow Execution with Confirmation

- **GIVEN** a user initiates a planning workflow requiring confirmation
- **WHEN** the orchestration proceeds
- **THEN** events are emitted in order:
  1. `intent.classified`
  2. `gate.selected` (with `requiresConfirmation: true`)
  3. `[user confirms]`
  4. `run.started`
  5. `phase.started`
  6. Zero or more (`tool.call`, `tool.result`) pairs
  7. `assistant.delta` (zero or more)
  8. `assistant.final`
  9. `phase.completed`
  10. `run.finished`

#### Scenario: Workflow Execution Rejected

- **GIVEN** a user initiates a planning workflow requiring confirmation
- **WHEN** the user rejects the proposed action
- **THEN** events are emitted in order:
  1. `intent.classified`
  2. `gate.selected` (with `requiresConfirmation: true`)
  3. `[user rejects]`
  4. `gate.rejected` event with rejection context
  5. `assistant.final` with alternative suggestions
  6. No `run.started` or `run.finished` events

## ADDED Requirements

### Requirement: Gate Rejection Event

The protocol MUST support a `gate.rejected` event when users decline a proposed action.

#### Scenario: User rejects destructive operation

- **GIVEN** a destructive workflow (e.g., executing tasks that modify files)
- **WHEN** the user rejects the proposed action at the gate
- **THEN** a `gate.rejected` event is emitted

**Event Structure**:
```json
{
  "type": "gate.rejected",
  "payload": {
    "targetPhase": "Executor",
    "rejectionReason": "User declined",
    "alternativeOptions": [
      { "type": "Chat", "description": "Discuss the plan first" },
      { "type": "ReadOnly", "description": "Preview what would change" }
    ]
  }
}
```
