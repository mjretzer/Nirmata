## ADDED Requirements

### Requirement: Orchestrator page provides unified subagent command interface

The `Gmsd.Web` project SHALL provide an `/Orchestrator` page that serves as the primary user interface for commanding subagents through the orchestration system.

The page MUST:
- Accept both CLI-style commands (e.g., `/status`, `/run`, `/validate`) and freeform natural language input
- Display a conversational message thread showing command history and responses
- Provide real-time feedback during subagent execution
- Show workspace context (selected workspace, status, current phase)
- Display safety rails and operation boundaries
- List recent evidence files and run artifacts

#### Scenario: User submits CLI command
- **GIVEN** the user is on the Orchestrator page
- **WHEN** they type `/status` and submit
- **THEN** the command is sent to the `IOrchestrator` via `DirectAgentRunner`
- **AND** the response appears in the message thread with structured formatting

#### Scenario: User submits freeform command
- **GIVEN** the user is on the Orchestrator page
- **WHEN** they type "analyze the codebase and identify issues" and submit
- **THEN** the freeform text is normalized to a run command and dispatched
- **AND** the subagent orchestrator processes the intent through the workflow engine

#### Scenario: Subagent execution shows progress
- **GIVEN** a subagent run is in progress
- **WHEN** the orchestrator reports phase transitions
- **THEN** the UI displays progress indicators and phase information in real-time

### Requirement: Orchestrator page integrates with DirectAgentRunner

The Orchestrator page model MUST use `DirectAgentRunner` for all orchestration requests.

The integration MUST:
- Inject `DirectAgentRunner` via constructor
- Call `ExecuteAsync()` with user input (CLI or freeform)
- Handle `OrchestratorResult` responses and render appropriate UI
- Display toast notifications for run start/complete/failure events
- Capture and display run artifacts in the message thread

#### Scenario: Successful subagent run displays results
- **GIVEN** a completed subagent execution via `DirectAgentRunner.ExecuteAsync()`
- **WHEN** the result indicates success
- **THEN** the UI shows run ID, final phase, and artifact summary in a formatted message

#### Scenario: Failed subagent run displays error details
- **GIVEN** a failed subagent execution
- **WHEN** the result indicates failure with error details
- **THEN** the UI displays an error message with the run ID and error information

### Requirement: Orchestrator page provides slash command autocomplete

The Orchestrator page MUST provide autocomplete for known CLI commands when the user types `/`.

The autocomplete MUST:
- Display when user types `/` in the input field
- Filter as user continues typing
- Support keyboard navigation (up/down arrows, Enter to select)
- Include command descriptions for discoverability

#### Scenario: Slash command autocomplete appears
- **GIVEN** the user has typed `/` in the command input
- **WHEN** autocomplete suggestions are available
- **THEN** a dropdown displays matching commands with descriptions

#### Scenario: Keyboard navigation of autocomplete
- **GIVEN** the autocomplete dropdown is visible
- **WHEN** the user presses Arrow Down then Enter
- **THEN** the highlighted command is inserted into the input field

### Requirement: Orchestrator page displays workspace context

The Orchestrator page MUST display the current workspace context and status.

The context display MUST include:
- Selected workspace name and path
- Current workspace status (initialized, uninitialized, error)
- Current cursor position (phase/task/step) if available
- Link to workspace selection page if none selected

#### Scenario: Workspace context displayed
- **GIVEN** a workspace is selected
- **WHEN** the page loads
- **THEN** the workspace indicator shows the workspace name with status badge

#### Scenario: Missing workspace shows warning
- **GIVEN** no workspace is selected
- **WHEN** the page loads
- **THEN** a warning message prompts the user to select a workspace first

### Requirement: Orchestrator page maintains message history

The Orchestrator page MUST persist message history between page loads.

Persistence MUST:
- Save message thread to local storage or application data folder
- Load previous messages on page initialization
- Maintain user/system message distinction
- Preserve timestamps and metadata

#### Scenario: Message history persists across reloads
- **GIVEN** a user has submitted commands and received responses
- **WHEN** they reload the page
- **THEN** the previous message thread is restored from storage


### Requirement: Direct Agent Runner in Web Project


The `Gmsd.Web` project SHALL provide a `DirectAgentRunner` class that enables in-process agent execution without requiring the Windows Service host.

The implementation MUST:
- Accept `IOrchestrator` via constructor injection
- Provide `ExecuteAsync(string inputRaw, CancellationToken)` method
- Generate correlation IDs for each execution
- Normalize inputs into `WorkflowIntent` for the orchestrator, supporting both CLI-style and freeform text
- Return `OrchestratorResult` with run status and artifacts
- Handle execution exceptions gracefully

#### Scenario: Execute CLI command via DirectAgentRunner
- **GIVEN** the Web application is configured with `AddGmsdAgents()`
- **WHEN** `DirectAgentRunner.ExecuteAsync("/status")` is called
- **THEN** the orchestrator executes without spawning external processes
- **AND** a valid `OrchestratorResult` is returned with run ID

#### Scenario: Execute freeform text via DirectAgentRunner
- **GIVEN** the Web application is configured with `AddGmsdAgents()`
- **WHEN** `DirectAgentRunner.ExecuteAsync("check the current state")` is called
- **THEN** the input is normalized and dispatched to the orchestrator
- **AND** a valid `OrchestratorResult` is returned


