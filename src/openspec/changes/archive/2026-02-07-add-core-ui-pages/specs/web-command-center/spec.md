## ADDED Requirements

### Requirement: Command Center Page

The `nirmata.Web` project SHALL provide a `/Command` page with a modern chat-style UI for interacting with the AOS engine.

The implementation MUST:
- Display chat thread with messages, streaming output indicators, and run cards
- Support slash commands: /init, /status, /validate, /spec, /run, /codebase, /pack, /checkpoint
- Show run summary per send: status, next action, evidence links
- Render inline: JSON artifacts, validation reports, command logs (syntax highlighted)
- Support attachments: evidence artifacts, linked files, context packs
- Display safety rails showing allowed scope and touched files for execution steps
- Use HTMX for message streaming and partial updates

#### Scenario: Send command and receive response
- **GIVEN** the chat interface is loaded
- **WHEN** a user types "/status" and submits
- **THEN** the message appears in the chat thread
- **AND** a response card appears with current engine status

#### Scenario: Display slash command help
- **GIVEN** the user types "/" in the input
- **WHEN** the slash is entered
- **THEN** an autocomplete dropdown shows available commands with descriptions
- **AND** selecting a command populates the input

#### Scenario: Show run summary with evidence links
- **GIVEN** a "/run" command completes successfully
- **WHEN** the run finishes
- **THEN** a run summary card appears showing: Status (✓), Next Action, Evidence links
- **AND** clicking an evidence link opens the artifact in `/Specs`

#### Scenario: Render JSON artifact inline
- **GIVEN** a command produces `summary.json`
- **WHEN** the response renders
- **THEN** the JSON is displayed with syntax highlighting
- **AND** it can be collapsed/expanded

#### Scenario: Display safety rails for execution
- **GIVEN** a command will modify files
- **WHEN** the command is being processed
- **THEN** a safety rail banner shows: "Scope: nirmata.Web/Pages/**", "Files to modify: 3"
- **AND** the user can review before confirming

#### Scenario: Attach evidence artifact
- **GIVEN** a user is composing a message
- **WHEN** they click the attachment button and select a file from `.aos/evidence/`
- **THEN** the attachment appears below the input
- **AND** it is included with the command context
