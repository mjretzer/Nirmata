# Spec Delta: Structured Command Proposals

## ADDED Requirements

### Requirement: Command Proposal Output Schema
The `LlmCommandSuggester` MUST produce a JSON object following a strict schema for every suggested next action.

#### Scenario: Valid Command Proposal Generation
- **Given** a run context and current state
- **When** the agent proposes a next action
- **Then** it MUST return a JSON object containing:
    - `intent`: An object describing the proposed action
    - `command`: The suggested command string (e.g., `/execute`)
    - `group`: The command group (e.g., `run`)
    - `rationale`: A short explanation of why this action is chosen
    - `expectedOutcome`: What the agent expects to happen after execution

### Requirement: Schema Validation for Proposals
The engine MUST validate every command proposal against the `CommandProposal` schema before presenting it to the user or executing it.

#### Scenario: Invalid Proposal Rejection
- **Given** a command proposal that violates the schema
- **When** the orchestrator receives the proposal
- **Then** it MUST reject the proposal and log a system error.
