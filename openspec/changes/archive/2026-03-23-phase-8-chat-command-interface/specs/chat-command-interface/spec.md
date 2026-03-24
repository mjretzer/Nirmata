# chat-command-interface Specification

## Purpose
Define the real `ChatPage` workspace command experience.

## ADDED Requirements

### Requirement: Role-aware message thread
The application SHALL render chat responses as a threaded conversation with distinct user, assistant, system, and result message styles.

#### Scenario: Render mixed message roles
- **WHEN** `ChatPage` receives a chat snapshot containing multiple roles
- **THEN** the page renders each message with the appropriate presentation and metadata

#### Scenario: Show streaming state
- **WHEN** a response is still in progress
- **THEN** the page shows an in-flight indicator and keeps the thread responsive

### Requirement: Command assistance surfaces
The application SHALL surface command suggestions and quick actions from the chat API.

#### Scenario: Autocomplete command suggestions
- **WHEN** a user types a partial `aos` command or slash prompt
- **THEN** `ChatPage` shows matching command suggestions from the backend snapshot

#### Scenario: Quick action button press
- **WHEN** a user activates a quick action
- **THEN** the page submits the associated command through the real chat flow

### Requirement: Orchestrator detail surfaces
The application SHALL display timeline steps and artifact changes from `OrchestratorMessage` responses.

#### Scenario: Timeline updates after a command
- **WHEN** a chat turn returns a timeline
- **THEN** the page renders each step in order with status updates

#### Scenario: Artifact changes are visible
- **WHEN** a chat turn returns artifact references
- **THEN** the page renders the affected artifact chips or references

### Requirement: Chat remains aligned with backend shapes
The application SHALL keep frontend chat types aligned with the API response schema.

#### Scenario: Response shape changes are mapped centrally
- **WHEN** the backend updates `OrchestratorMessage`
- **THEN** the frontend mapping layer is the only place that needs to adapt the raw payload
