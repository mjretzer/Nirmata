# destructive-operation-detection Specification

## Purpose
Define the detection and classification of destructive operations that require explicit user confirmation before execution, including file system mutations and git operations.

## ADDED Requirements

### Requirement: Destructiveness Analysis for File System Operations
The system SHALL analyze proposed file system operations to determine their destructiveness level.

#### Scenario: File creation is write-safe
- **GIVEN** a proposed operation that creates new files in `.aos/`
- **WHEN** the destructiveness analyzer evaluates the operation
- **THEN** it SHALL classify as `RiskLevel.WriteSafe`
- **AND** confirmation MAY be skipped based on confidence

#### Scenario: File modification is write-destructive
- **GIVEN** a proposed operation that modifies existing source files
- **WHEN** the destructiveness analyzer evaluates the operation
- **THEN** it SHALL classify as `RiskLevel.WriteDestructive`
- **AND** require explicit confirmation before proceeding

#### Scenario: File deletion is write-destructive
- **GIVEN** a proposed operation that deletes files
- **WHEN** the destructiveness analyzer evaluates the operation
- **THEN** it SHALL classify as `RiskLevel.WriteDestructive`
- **AND** the confirmation request SHALL highlight deletion risk

#### Scenario: Git repository mutation is write-destructive-git
- **GIVEN** a proposed git commit or push operation
- **WHEN** the destructiveness analyzer evaluates the operation
- **THEN** it SHALL classify as `RiskLevel.WriteDestructiveGit`
- **AND** require explicit confirmation with irreversibility warning

### Requirement: Git Operation Destructiveness Classification
The system SHALL specifically detect and classify git operations based on their mutability and reversibility.

#### Scenario: Git commit requires confirmation
- **GIVEN** a proposed git commit operation
- **WHEN** the analyzer evaluates
- **THEN** it SHALL require confirmation
- **AND** the confirmation message SHALL warn that commits are recorded permanently

#### Scenario: Git push requires high-threshold confirmation
- **GIVEN** a proposed git push operation
- **WHEN** the analyzer evaluates
- **THEN** it SHALL require confirmation with elevated risk messaging
- **AND** the confirmation SHALL display the remote and branch being pushed to

#### Scenario: Git status is read-only
- **GIVEN** a proposed git status or log operation
- **WHEN** the analyzer evaluates
- **THEN** it SHALL classify as `RiskLevel.Read`
- **AND** NOT require confirmation

#### Scenario: Git stash is write-destructive
- **GIVEN** a proposed git stash operation
- **WHEN** the analyzer evaluates
- **THEN** it SHALL classify as `RiskLevel.WriteDestructive`
- **AND** require confirmation with explanation of work-in-progress impact

### Requirement: Scope-Aware Destructiveness Analysis
The system SHALL consider the scope of affected resources when determining destructiveness.

#### Scenario: Single file modification
- **GIVEN** a proposed operation affecting 1 source file
- **WHEN** the analyzer evaluates
- **THEN** the risk level SHALL be `WriteDestructive`
- **AND** the confirmation SHALL list the single affected file

#### Scenario: Multiple file modifications
- **GIVEN** a proposed operation affecting 10+ source files
- **WHEN** the analyzer evaluates
- **THEN** the risk level SHALL be `WriteDestructive`
- **AND** the confirmation SHALL display count and sample of affected files

#### Scenario: Workspace-critical file modification
- **GIVEN** a proposed operation affecting `.aos/state.json` or `.aos/spec.json`
- **WHEN** the analyzer evaluates
- **THEN** the risk level SHALL be `RiskLevel.WorkspaceDestructive`
- **AND** require explicit confirmation with workspace integrity warning

### Requirement: Destructiveness Analyzer Integration with Gating Engine
The system SHALL wire destructiveness analysis into the gating engine for all phase transitions.

#### Scenario: Executor phase triggers destructive analysis
- **GIVEN** the gating engine routes to "Executor" phase
- **WHEN** the `GatingResult` is constructed
- **THEN** the `IDestructivenessAnalyzer` SHALL evaluate the phase
- **AND** set `RiskLevel` and `RequiresConfirmation` accordingly

#### Scenario: Planner phase is write-safe
- **GIVEN** the gating engine routes to "Planner" phase
- **WHEN** the destructiveness analyzer evaluates
- **THEN** it SHALL return `RiskLevel.WriteSafe`
- **AND** `RequiresConfirmation = false` (unless replanning after execution)

#### Scenario: FixPlanner phase after execution requires confirmation
- **GIVEN** the gating engine routes to "FixPlanner" phase
- **AND** the `LastExecutionStatus` is "completed"
- **WHEN** the destructiveness analyzer evaluates
- **THEN** it SHALL require confirmation for replanning

### Requirement: Destructiveness Reporting in GatingResult
The system SHALL include destructiveness information in the gating result for downstream consumers.

#### Scenario: GatingResult includes risk assessment
- **GIVEN** any phase routing decision
- **WHEN** the `GatingResult` is returned
- **THEN** it SHALL include:
  - `RiskLevel`: The assessed risk level
  - `RequiresConfirmation`: Whether confirmation is needed
  - `SideEffects`: List of affected systems (file_system, external_process, git)
  - `ProposedAction.AffectedResources`: Specific resources at risk

#### Scenario: ProposedAction describes destructive impact
- **GIVEN** a `WriteDestructive` or `WriteDestructiveGit` classification
- **WHEN** the `ProposedAction` is constructed
- **THEN** the `Description` SHALL clearly state the destructive nature
- **AND** include specific consequences of the operation

### Requirement: Configuration-Based Destructiveness Overrides
The system SHALL support configuration overrides for destructiveness classification.

#### Scenario: User configures always-confirm-git
- **GIVEN** a configuration setting `AlwaysConfirmGit: true`
- **WHEN** any git operation is evaluated
- **THEN** it SHALL always require confirmation regardless of other factors

#### Scenario: User configures never-confirm-planner
- **GIVEN** a configuration setting `Planner.NoConfirmation: true`
- **WHEN** the Planner phase is evaluated
- **THEN** it SHALL skip confirmation for planning operations

#### Scenario: Workspace-level destructiveness config
- **GIVEN** a `.aos/config/destructiveness.json` file exists
- **WHEN** the analyzer initializes
- **THEN** it SHALL load workspace-specific overrides
- **AND** apply them to subsequent evaluations

## Cross-References

- Related to: `confirmation-gate` spec (uses destructiveness analysis)
- Related to: `aos-policy-enforcement` (policy-driven destructiveness rules)
- Related to: `agents-atomic-git-committer` (git-specific handling)
- Related to: `aos-execute-plan` (execution scope enforcement)
