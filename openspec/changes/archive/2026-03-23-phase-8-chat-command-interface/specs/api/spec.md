## MODIFIED Requirements

### Requirement: Chat Integration
The API MUST provide workspace-scoped chat messages, command suggestions, quick actions, and structured orchestrator responses.

#### Scenario: Get chat snapshot
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/chat`
- **THEN** the API returns the current thread snapshot, command suggestions, and quick actions for that workspace

#### Scenario: Send chat turn
- **WHEN** a client posts freeform text to `POST /v1/workspaces/{workspaceId}/chat`
- **THEN** the API classifies the input, executes the resulting command flow, and returns an `OrchestratorMessage`
- **AND** the response includes the role, content, gate, artifacts, timeline, and `nextCommand` fields
- **AND** the response may be delivered as a streamed or polling-friendly payload

#### Scenario: Unknown workspace
- **WHEN** a client requests chat data for an unknown workspace
- **THEN** the API returns `404 Not Found`
