## ADDED Requirements
### Requirement: Command proposals are versioned and schema-addressable
Command proposal artifacts MUST include a declared `schemaVersion` and be validated against a canonical command-proposal schema `$id`.

#### Scenario: Command proposal includes schema version metadata
- **GIVEN** the command suggester emits a proposal
- **WHEN** the proposal artifact is produced
- **THEN** it includes a supported `schemaVersion` and resolves to the canonical command-proposal schema identity

### Requirement: Invalid command proposal contracts produce actionable diagnostics
When command proposal validation fails, the orchestrator MUST fail the proposal with diagnostic details that can be surfaced in logs and UI.

#### Scenario: Command proposal schema mismatch returns diagnostic details
- **GIVEN** a proposal payload that violates the canonical command-proposal schema
- **WHEN** orchestrator validation executes
- **THEN** the proposal is rejected with diagnostics including schema `$id`, instance location, and human-readable message
