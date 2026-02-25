## MODIFIED Requirements
### Requirement: Orchestrator implements "classify → gate → dispatch → validate → persist → next" workflow with unified chat/command handling

The system SHALL provide an `IOrchestrator` interface in `Gmsd.Agents.Execution.Orchestrator` that serves as the unified entry point for both chat and command workflow execution.

The implementation MUST:
- Accept `WorkflowIntent` via `ExecuteAsync` method with enhanced intent classification
- Start run lifecycle via `IRunLifecycleManager` for both chat and command paths
- Build gating context from workspace state and input type
- Evaluate gates via `IGatingEngine` with chat vs command routing logic
- Dispatch to phase handlers via `ICommandRouter` or chat responder as appropriate
- Close run with status and outputs for all interaction types
- Return `OrchestratorResult` with success/failure, final phase, run ID, and artifacts

#### Scenario: Orchestrator executes unified chat/command workflow loop
- **GIVEN** a workspace with valid project, roadmap, and plan
- **WHEN** `ExecuteAsync` is called with a conversational intent (no slash prefix)
- **THEN** the orchestrator routes to chat responder → returns LLM-generated response
- **AND** the response includes workspace context and optional command suggestions

#### Scenario: Orchestrator handles explicit command execution
- **GIVEN** a workspace and user input with slash prefix
- **WHEN** `ExecuteAsync` is called with command intent
- **THEN** the orchestrator follows traditional workflow → starts run → executes command → returns result
- **AND** the result includes execution artifacts and status

#### Scenario: Orchestrator handles command suggestion confirmation
- **GIVEN** a chat response that includes a command proposal
- **WHEN** the user confirms the proposal
- **THEN** the orchestrator executes the suggested command as a separate workflow
- **AND** both the original chat and command execution are recorded in evidence

---

### Requirement: Gating engine evaluates unified routing logic with chat detection

The system SHALL provide an `IGatingEngine` interface that evaluates workspace state and input type for unified routing:

1. **Chat input (no slash prefix)** → route to **Chat Responder** with workspace context
2. **Command input (slash prefix)** → evaluate traditional 6-phase routing logic:
   - Missing project spec → route to **Interviewer**
   - Missing roadmap → route to **Roadmapper**
   - Missing phase plan → route to **Planner**
   - Ready to execute → route to **Executor**
   - Execution complete, verification pending → route to **Verifier**
   - Verification failed → route to **FixPlanner**

#### Scenario: Gating routes to Chat Responder for conversational input
- **GIVEN** user input without slash prefix
- **WHEN** `EvaluateAsync` is called
- **THEN** result indicates `TargetPhase: ChatResponder` with reason "Conversational input detected"
- **AND** the result includes workspace context for the chat response

#### Scenario: Gating routes to traditional workflow for commands
- **GIVEN** user input with slash prefix like `/run`
- **WHEN** `EvaluateAsync` is called
- **THEN** result follows traditional 6-phase routing logic
- **AND** the appropriate workflow phase is selected based on workspace state

#### Scenario: Gating handles command suggestions from chat
- **GIVEN** a chat response that includes command proposals
- **WHEN** a proposal is confirmed by the user
- **THEN** gating evaluates the proposed command using traditional routing logic
- **AND** the command executes in the appropriate workflow phase

---

## ADDED Requirements
### Requirement: Command parser integration for unified input processing

The orchestrator SHALL integrate with a command parser to distinguish between chat and command inputs at the entry point.

#### Scenario: Input type classification at orchestrator entry

- **GIVEN** any user input received by the orchestrator
- **WHEN** `ExecuteAsync` is called
- **THEN** the input is parsed to determine if it's a command (slash prefix) or chat (no prefix)
- **AND** the intent classification includes the detected input type

#### Scenario: Command validation and routing

- **GIVEN** input identified as a command
- **WHEN** the command parser processes it
- **THEN** the command is validated against the command registry
- **AND** valid commands are routed to appropriate workflow phases
- **AND** invalid commands return error responses with help text

#### Scenario: Chat input detection and handling

- **GIVEN** input identified as chat (no slash prefix)
- **WHEN** the input is processed
- **THEN** it's routed directly to the chat responder
- **AND** workspace context is assembled for the response
- **AND** the response may include command suggestions

---

### Requirement: Unified evidence capture for mixed interactions

The orchestrator SHALL maintain a single evidence trail that captures both chat conversations and command executions in chronological order.

#### Scenario: Evidence folder includes chat interactions

- **GIVEN** a conversation that includes both chat and command interactions
- **WHEN** the orchestrator processes the interactions
- **THEN** the evidence folder SHALL contain records of all interactions
- **AND** chat messages SHALL be stored with metadata (timestamp, type, context)
- **AND** command executions SHALL be stored with traditional evidence artifacts

#### Scenario: Run lifecycle supports mixed interaction types

- **GIVEN** a single run that includes both chat and command interactions
- **WHEN** the run is managed by `IRunLifecycleManager`
- **THEN** the run SHALL track both chat messages and command executions
- **AND** the run summary SHALL include statistics for both interaction types
- **AND** the run SHALL be resumable at any point in the mixed interaction flow

#### Scenario: Conversation state persistence across runs

- **GIVEN** multiple runs that occur within the same conversation session
- **WHEN** subsequent runs are started
- **THEN** conversation context from previous runs SHALL be available
- **AND** the chat responder SHALL reference earlier interactions when relevant
- **AND** command suggestions SHALL consider the full conversation history

---

### Requirement: Error handling for unified chat/command flow

The orchestrator SHALL provide comprehensive error handling that gracefully manages failures in both chat and command paths without breaking the user experience.

#### Scenario: Chat responder failure fallback

- **GIVEN** the chat responder fails (LLM provider unavailable, context errors)
- **WHEN** a chat request is being processed
- **THEN** the orchestrator SHALL return a friendly fallback message
- **AND** the fallback SHALL include basic help information and command suggestions
- **AND** the error SHALL be logged with full context for debugging

#### Scenario: Command execution failure with chat context

- **GIVEN** a command execution fails during a conversation
- **WHEN** the error occurs
- **THEN** the orchestrator SHALL return an error response in conversational format
- **AND** the response SHALL explain what went wrong and suggest next steps
- **AND** the conversation SHALL continue without requiring restart

#### Scenario: Mixed interaction error recovery

- **GIVEN** an error occurs during a mixed chat/command interaction
- **WHEN** the orchestrator handles the error
- **THEN** it SHALL maintain conversation context where possible
- **AND** it SHALL provide clear guidance on how to proceed
- **AND** it SHALL preserve any successful work completed before the error

#### Scenario: Evidence capture for chat interactions

- **GIVEN** a conversation that includes both chat and command interactions
- **WHEN** the orchestrator processes the interactions
- **THEN** the evidence folder SHALL contain records of all interactions
- **AND** chat messages SHALL be stored with metadata (timestamp, type, context)
- **AND** command executions SHALL be stored with traditional evidence artifacts
